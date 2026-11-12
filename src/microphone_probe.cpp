#include <windows.h>
#include <mmsystem.h>
#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <chrono>
#include <condition_variable>
#include <cstdint>
#include <cstring>
#include <deque>
#include <filesystem>
#include <iomanip>
#include <mutex>
#include <sstream>
#include <string>
#include <thread>
#include <vector>
#include <rex/assert.h>
#include <rex/cvar.h>
#include <rex/hook.h>
#include <rex/logging.h>
#include <rex/system/kernel_state.h>
#include <rex/system/thread_state.h>
#include <rex/system/xobject.h>

REXCVAR_DEFINE_BOOL(trace_mic_device_requests, false, "Diagnostics", "Log a bounded trace of Xbox microphone device requests");
REXCVAR_DEFINE_BOOL(virtual_microphone, false, "Input", "Expose the default Windows recording device as an Xbox 360 USB microphone");
REXCVAR_DEFINE_INT32(virtual_microphone_port, 0, "Input", "Controller port (0-3) that reports a microphone");
REXCVAR_DEFINE_STRING(virtual_microphone_device, "", "Input", "Recording device name for the virtual microphone; empty uses the Windows default");
REXCVAR_DEFINE_INT32(virtual_microphone_gain, 100, "Input", "Virtual microphone input gain in percent (0-400)");
REXCVAR_DEFINE_INT32(virtual_microphone_latency_ms, 5, "Input", "Virtual microphone capture buffer length in milliseconds (5-100); lower means less delay");
REXCVAR_DEFINE_BOOL(virtual_microphone_fast_poll, true, "Input", "Also deliver microphone data from the audio render thread (every ~5 ms) instead of only when the title polls its controllers");

// The title's XMic library talks USB Audio Class straight to MicDeviceRequest.
// Request layout (R = the pointer in r3, all fields big-endian):
//   +0 u16 command  +2 u16 device id  +12 context  +16 completion routine
//   +20 data pointer / value  +24 length / count  +28..+35 USB setup packet
//   -8 pointer to the title's device object (+4 sample rate, +8 bytes per ms)
// Commands: 0 query state, 1 start, 2 control transfer, 4 configure, 5 stop,
// 7 isochronous read, 8 query supported sample rates.
namespace {
constexpr std::uint32_t kStatusPending = 0x103;
constexpr std::uint32_t kContextMic = 0x60000000;
constexpr std::uint32_t kSupportedRates = 64 | 256;
constexpr std::uint32_t kMaxIsoBytes = 768;
constexpr std::uint32_t kStateIdle = 1;
constexpr std::uint32_t kStateStreaming = 5;

std::uint16_t Rd16(std::uint8_t* base, std::uint32_t a) { std::uint16_t v; std::memcpy(&v, base + a, 2); return _byteswap_ushort(v); }
std::uint32_t Rd32(std::uint8_t* base, std::uint32_t a) { std::uint32_t v; std::memcpy(&v, base + a, 4); return _byteswap_ulong(v); }
void Wr16(std::uint8_t* base, std::uint32_t a, std::uint16_t v) { v = _byteswap_ushort(v); std::memcpy(base + a, &v, 2); }
void Wr32(std::uint8_t* base, std::uint32_t a, std::uint32_t v) { v = _byteswap_ulong(v); std::memcpy(base + a, &v, 4); }

std::filesystem::path Utf8Path(const std::string& value) {
  std::u8string utf8;
  utf8.reserve(value.size());
  for (const unsigned char byte : value) utf8.push_back(static_cast<char8_t>(byte));
  return std::filesystem::path(utf8);
}

PPCFunc* Original(const char* name) {
  auto* original = reinterpret_cast<PPCFunc*>(GetProcAddress(GetModuleHandleW(L"rexruntime.dll"), name));
  if (!original) REX_FATAL("The runtime has no {} export", name);
  return original;
}

std::string Snapshot(std::uint8_t* base, std::uint32_t address) {
  if (!address) return "null";
  std::ostringstream output;
  for (unsigned offset = 0; offset < 16; ++offset) {
    if (offset) output << ' ';
    output << std::hex << std::setw(2) << std::setfill('0')
           << unsigned(*(base + address + offset));
  }
  return output.str();
}

class Capture {
 public:
  static unsigned LatencyMs() { return static_cast<unsigned>(std::clamp(REXCVAR_GET(virtual_microphone_latency_ms), 5, 100)); }
  ~Capture() { Stop(); }

  bool Start(unsigned rate) {
    Stop();
    if (!rate) {
      REXLOG_ERROR("MIC: refusing to start capture with a zero sample rate");
      return false;
    }
    rate_ = rate;
    return StartWaveIn(rate);
  }

  bool StartWaveIn(unsigned rate) {
    WAVEFORMATEX format{};
    format.wFormatTag = WAVE_FORMAT_PCM;
    format.nChannels = 1;
    format.nSamplesPerSec = rate;
    format.wBitsPerSample = 16;
    format.nBlockAlign = 2;
    format.nAvgBytesPerSec = rate * 2;
    event_ = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    if (!event_) {
      REXLOG_WARN("MIC: could not create the waveIn capture event ({})",
          unsigned(GetLastError()));
      return false;
    }
    UINT device_id = WAVE_MAPPER;
    const auto wanted = Utf8Path(REXCVAR_GET(virtual_microphone_device)).wstring();
    if (!wanted.empty()) {
      for (UINT i = 0; i < waveInGetNumDevs(); ++i) {
        WAVEINCAPSW caps{};
        if (waveInGetDevCapsW(i, &caps, sizeof(caps)) == MMSYSERR_NOERROR && wanted == caps.szPname) {
          device_id = i;
          break;
        }
      }
      if (device_id == WAVE_MAPPER) REXLOG_WARN("MIC: recording device '{}' not found; using the Windows default", REXCVAR_GET(virtual_microphone_device));
    }
    const auto result = waveInOpen(&device_, device_id, &format, reinterpret_cast<DWORD_PTR>(event_), 0, CALLBACK_EVENT);
    if (result != MMSYSERR_NOERROR) {
      REXLOG_WARN("MIC: waveInOpen failed ({}); the game will receive silence", unsigned(result));
      CloseHandle(event_);
      event_ = nullptr;
      device_ = nullptr;
      return false;
    }
    const DWORD bytes = std::max<DWORD>(2, (rate * LatencyMs() / 1000) * 2);
    for (auto& block : blocks_) {
      block.data.assign(bytes, 0);
      block.header = {};
      block.header.lpData = reinterpret_cast<LPSTR>(block.data.data());
      block.header.dwBufferLength = bytes;
      waveInPrepareHeader(device_, &block.header, sizeof(WAVEHDR));
      waveInAddBuffer(device_, &block.header, sizeof(WAVEHDR));
    }
    running_ = true;
    thread_ = std::thread([this] { Pump(); });
    waveInStart(device_);
    return true;
  }

  void Stop() {
    if (device_) {
      running_ = false;
      waveInReset(device_);
      SetEvent(event_);
      if (thread_.joinable()) thread_.join();
      for (auto& block : blocks_) waveInUnprepareHeader(device_, &block.header, sizeof(WAVEHDR));
      waveInClose(device_);
      CloseHandle(event_);
      device_ = nullptr;
      event_ = nullptr;
    }
    std::lock_guard lock(mutex_);
    ring_.clear();
  }

  bool Active() const { return device_ != nullptr; }
  std::uint64_t TakeDropped() { return dropped_.exchange(0); }

  std::size_t Available() {
    std::lock_guard lock(mutex_);
    return ring_.size();
  }

  // Copies up to `samples` int16 samples, waiting at most wait_ms for them.
  std::size_t Read(std::int16_t* out, std::size_t samples, unsigned wait_ms) {
    std::unique_lock lock(mutex_);
    ready_.wait_for(lock, std::chrono::milliseconds(wait_ms), [&] { return ring_.size() >= samples; });
    const auto count = std::min(samples, ring_.size());
    std::copy_n(ring_.begin(), count, out);
    ring_.erase(ring_.begin(), ring_.begin() + count);
    return count;
  }

 private:
  struct Block { std::vector<std::uint8_t> data; WAVEHDR header{}; };

  void Pump() {
    while (running_) {
      WaitForSingleObject(event_, 50);
      for (auto& block : blocks_) {
        if (!(block.header.dwFlags & WHDR_DONE)) continue;
        const auto* pcm = reinterpret_cast<const std::int16_t*>(block.data.data());
        {
          std::lock_guard lock(mutex_);
          ring_.insert(ring_.end(), pcm, pcm + block.header.dwBytesRecorded / 2);
          if (ring_.size() > rate_ * std::max(LatencyMs() * 3, 24u) / 1000) {
            const auto keep = rate_ * LatencyMs() / 1000;
            dropped_ += ring_.size() - keep;
            ring_.erase(ring_.begin(), ring_.end() - keep);
          }
        }
        ready_.notify_all();
        if (running_) waveInAddBuffer(device_, &block.header, sizeof(WAVEHDR));
      }
    }
  }

  HWAVEIN device_ = nullptr;
  HANDLE event_ = nullptr;
  unsigned rate_ = 32000;
  std::array<Block, 4> blocks_{};
  std::thread thread_;
  std::atomic<bool> running_{false};
  std::atomic<std::uint64_t> dropped_{0};
  std::mutex mutex_;
  std::condition_variable ready_;
  std::deque<std::int16_t> ring_;
};

Capture g_capture;
std::atomic<std::uint32_t> g_state{kStateIdle};
std::uint32_t g_scratch = 0;

struct PendingRead {
  std::uint32_t request;
  std::chrono::steady_clock::time_point due;
};
std::mutex g_pending_mutex;
std::deque<PendingRead> g_pending;
std::chrono::steady_clock::time_point g_next_due;
thread_local bool t_draining = false;
std::atomic<float> g_game_gain{1.0f};
std::atomic<bool> g_game_mute{false};

// Applies the launcher gain and the gain the title requested through the USB
// volume control (or mutes when the title asks for it).
void ProcessSamples(std::int16_t* samples, std::size_t count) {
  if (!count) return;
  const float gain = g_game_mute.load() ? 0.0f
      : std::clamp(REXCVAR_GET(virtual_microphone_gain), 0, 400) / 100.0f * g_game_gain.load();
  if (gain == 1.0f) return;
  for (std::size_t i = 0; i < count; ++i)
    samples[i] = static_cast<std::int16_t>(std::clamp(samples[i] * gain, -32768.0f, 32767.0f));
}

void TraceRead(const char* what, std::uint32_t request, std::uint32_t callback) {
  if (!REXCVAR_GET(trace_mic_device_requests)) return;
  static std::atomic_uint logged{};
  if (logged.fetch_add(1, std::memory_order_relaxed) >= 40) return;
  REXLOG_INFO("MICDBG {}: request={:08X} callback={:08X}", what, request, callback);
}

void CallCompletion(std::uint32_t request, std::uint32_t callback) {
  std::uint64_t arguments[] = {request, 0, g_scratch, g_scratch + 32};
  rex::system::kernel_state()->function_dispatcher()->ExecuteTrap(
      rex::runtime::ThreadState::Get(), callback, arguments, 4);
}

// Real hardware completes isochronous reads asynchronously, 1 ms of audio per
// packet. Completing them inside the request would recurse without bound since
// the title resubmits from its completion routine, so they are paced here.
std::atomic_flag g_draining = ATOMIC_FLAG_INIT;
struct MicStats {
  std::uint64_t reads = 0, short_reads = 0, silent_samples = 0;
  std::chrono::steady_clock::time_point report{};
};
MicStats g_stats;

void ReportStats(std::size_t available) {
  const auto now = std::chrono::steady_clock::now();
  if (g_stats.report.time_since_epoch().count() == 0) g_stats.report = now;
  if (now - g_stats.report < std::chrono::seconds(5)) return;
  g_stats.report = now;
  if (!g_stats.reads) return;
  REXLOG_INFO("MIC stats: reads={} underruns={} silent_ms={:.1f} dropped_ms={:.1f} backlog_ms={:.1f} gain={}% capture_block={}ms",
              g_stats.reads, g_stats.short_reads, g_stats.silent_samples / 48.0, g_capture.TakeDropped() / 48.0,
              available / 48.0, REXCVAR_GET(virtual_microphone_gain), Capture::LatencyMs());
  g_stats = MicStats{};
  g_stats.report = now;
}

void DrainReads(std::uint8_t* base) {
  if (t_draining || !g_scratch) return;
  // The completion arguments live in one shared scratch area, so only one thread delivers at a time.
  if (g_draining.test_and_set(std::memory_order_acquire)) return;
  t_draining = true;
  for (int completed = 0; completed < 32; ++completed) {
    PendingRead item;
    {
      std::lock_guard lock(g_pending_mutex);
      if (g_pending.empty()) break;
      item = g_pending.front();
      if (item.due > std::chrono::steady_clock::now()) break;
      g_pending.pop_front();
    }
    const auto request = item.request;
    const auto device = Rd32(base, request - 8);
    const auto buffer = Rd32(base, request + 20);
    const auto count = std::clamp<std::uint32_t>(Rd32(base, request + 24), 1, 8);
    auto packet = device ? Rd32(base, device + 8) : 0;
    if (!packet) packet = 64;
    packet = std::min(packet, kMaxIsoBytes / count);
    const auto samples = packet * count / 2;
    auto* destination = reinterpret_cast<std::int16_t*>(base + buffer);
    std::size_t got = 0;
    if (g_capture.Active()) got = g_capture.Read(destination, samples, 0);
    ProcessSamples(destination, got);
    std::memset(destination + got, 0, (samples - got) * 2);
    ++g_stats.reads;
    if (got < samples) { ++g_stats.short_reads; g_stats.silent_samples += samples - got; }
    for (std::uint32_t i = 0; i < count; ++i) {
      Wr32(base, g_scratch + i * 4, 0);
      Wr16(base, g_scratch + 32 + i * 2, static_cast<std::uint16_t>(packet));
    }
    if (const auto callback = Rd32(base, request + 16)) {
      TraceRead("read complete begin", request, callback);
      CallCompletion(request, callback);
      TraceRead("read complete end", request, callback);
    }
  }
  if (g_capture.Active()) ReportStats(g_capture.Available());
  t_draining = false;
  g_draining.clear(std::memory_order_release);
}

void LogRequest(const char* what, std::uint32_t command, std::uint32_t request, std::uint32_t result) {
  if (!REXCVAR_GET(trace_mic_device_requests)) return;
  static std::atomic_uint logged{};
  if (logged.fetch_add(1, std::memory_order_relaxed) >= 256) return;
  REXLOG_INFO("MIC {}: command={}, address={:08X}, result={:08X}", what, command, request, result);
}

std::uint32_t EmulateMicDeviceRequest(std::uint8_t* base, std::uint32_t request) {
  const auto command = Rd16(base, request);
  if (command == 0) {
    Wr32(base, request + 4, g_state.load());
    LogRequest("query", command, request, 0);
    return 0;
  }
  const auto device = Rd32(base, request - 8);
  switch (command) {
    case 1: {
      auto rate = device ? Rd32(base, device + 4) : 0;
      if (rate < 8000 || rate > 96000) rate = 32000;
      g_capture.Start(rate);
      g_state = kStateStreaming;
      REXLOG_INFO("MIC: capture started at {} Hz, gain {}%, capture block {} ms", rate, REXCVAR_GET(virtual_microphone_gain), Capture::LatencyMs());
      break;
    }
    case 2: {
      static std::atomic_uint control_logged{};
      if (control_logged.fetch_add(1, std::memory_order_relaxed) < 24)
        REXLOG_INFO("MIC: control transfer type={:02X} request={:02X} value={:02X}{:02X} index={:02X}{:02X} length={}",
                    unsigned(base[request + 28]), unsigned(base[request + 29]), unsigned(base[request + 31]), unsigned(base[request + 30]),
                    unsigned(base[request + 33]), unsigned(base[request + 32]), Rd16(base, request + 34));
    }
    if (!(base[request + 28] & 0x80) && base[request + 29] == 0x01 && Rd32(base, request + 20)) {
        // SET_CUR on the audio feature unit: selector 1 is mute, 2 is volume (1/256 dB).
        const auto selector = base[request + 31];
        const auto* data = base + Rd32(base, request + 20);
        if (selector == 2) {
          const float db = std::clamp(static_cast<float>(static_cast<std::int16_t>(data[0] | (data[1] << 8))) / 256.0f, -12.0f, 12.0f);
          g_game_gain = std::pow(10.0f, db / 20.0f);
          REXLOG_INFO("MIC: title set volume to {:.2f} dB", db);
        } else if (selector == 1) {
          g_game_mute = data[0] != 0;
          REXLOG_INFO("MIC: title set mute={}", data[0] != 0);
        }
      }
      if (Rd32(base, request + 20) && (base[request + 28] & 0x80)) {
        std::uint8_t data[2]{};
        switch (base[request + 29]) {
          case 0x82: data[0] = 0x00; data[1] = 0xF4; break;  // minimum volume, -12 dB
          case 0x83: data[0] = 0x40; data[1] = 0x23; break;  // maximum volume, +35.25 dB
          case 0x84: data[0] = 0x00; data[1] = 0x01; break;  // resolution
          default: break;                                    // current volume: 0 dB
        }
        const auto length = std::min<std::uint32_t>(Rd16(base, request + 34), 2);
        std::memcpy(base + Rd32(base, request + 20), data, length);
      }
      break;
    case 4:
      break;
    case 5:
      g_capture.Stop();
      g_state = kStateIdle;
      REXLOG_INFO("MIC: capture stopped");
      break;
    case 7: {
      std::lock_guard lock(g_pending_mutex);
      const auto count = std::clamp<std::uint32_t>(Rd32(base, request + 24), 1, 8);
      const auto now = std::chrono::steady_clock::now();
      if (g_next_due < now - std::chrono::milliseconds(50)) g_next_due = now;
      g_next_due += std::chrono::milliseconds(count);
      g_pending.push_back({request, g_next_due});
      LogRequest("queued", command, request, kStatusPending);
      return kStatusPending;
    }
    case 8:
      Wr32(base, request + 20, kSupportedRates);
      break;
    default:
      LogRequest("unhandled", command, request, 0);
      return 0;
  }
  LogRequest("handled", command, request, kStatusPending);

  const auto callback = Rd32(base, request + 16);
  if (!callback) return 0;
  CallCompletion(request, callback);
  return kStatusPending;
}
}

void PumpMicrophone(std::uint8_t* base) {
  if (REXCVAR_GET(virtual_microphone)) DrainReads(base);
}

void PumpMicrophoneFromAudio(std::uint8_t* base) {
  if (REXCVAR_GET(virtual_microphone) && REXCVAR_GET(virtual_microphone_fast_poll)) DrainReads(base);
}

void Trace(const char* what, std::uint32_t a, std::uint32_t b, std::uint32_t c);

REX_HOOK_RAW(__imp__ObDereferenceObject) {
  // The SDK's ObReferenceObject is a no-op but ObDereferenceObject really
  // releases, so the XMic completion path (which dereferences the thread it
  // never really referenced) would destroy the calling thread. Thread
  // dispatch headers (type 6) are therefore ignored.
  static auto* original = Original("__imp__ObDereferenceObject");
  const auto pointer = ctx.r3.u32;
  const bool valid = pointer >= 0x10000 && pointer != 0xDEADF00D;
  const auto current = Rd32(base, ctx.r13.u32 + 256);
  Trace("ObDeref", pointer, valid ? base[pointer] : 0xFF, current);
  const bool is_thread = valid && (base[pointer] == 6 || pointer == current);
  if (REXCVAR_GET(virtual_microphone) && is_thread) {
    ctx.r3.u64 = 0;
    return;
  }
  original(ctx, base);
}

void Trace(const char* what, std::uint32_t a, std::uint32_t b, std::uint32_t c) {
  if (!REXCVAR_GET(trace_mic_device_requests)) return;
  static std::atomic_uint logged{};
  if (logged.fetch_add(1, std::memory_order_relaxed) >= 200) return;
  REXLOG_INFO("MICDBG {}: {:08X} {:08X} {:08X}", what, a, b, c);
}

REX_HOOK_RAW(__imp__ExFreePool) {
  static auto* original = Original("__imp__ExFreePool");
  Trace("ExFreePool", ctx.r3.u32, static_cast<std::uint32_t>(ctx.lr), 0);
  if (!ctx.r3.u32) return;
  original(ctx, base);
}

REX_HOOK_RAW(__imp__KeInsertQueueApc) {
  static auto* original = Original("__imp__KeInsertQueueApc");
  const auto apc = ctx.r3.u32;
  Trace("InsertApc", apc, apc ? Rd32(base, apc + 4) : 0, static_cast<std::uint32_t>(ctx.lr));
  original(ctx, base);
  Trace("InsertApc result", ctx.r3.u32, 0, 0);
}

REXCVAR_DEFINE_STRING(user_display_name, "", "Input", "Gamertag shown by the game for player 1; empty keeps the default");

// Only the displayed name changes; the profile folder keeps its original name so saves stay valid.
static std::string DisplayName() {
  auto name = REXCVAR_GET(user_display_name);
  if (name.size() > 15) name.resize(15);
  return name;
}

REX_HOOK_RAW(__imp__XamUserGetName) {
  static auto* original = Original("__imp__XamUserGetName");
  static std::atomic<bool> reported{false};
  const auto user = ctx.r3.u32, buffer = ctx.r4.u32, length = ctx.r5.u32;
  original(ctx, base);
  const auto name = DisplayName();
  const bool apply = !name.empty() && user == 0 && ctx.r3.u32 == 0 && buffer && length;
  if (apply) {
    const auto count = std::min<std::uint32_t>(length, 16);
    for (std::uint32_t i = 0; i < count; ++i) base[buffer + i] = i < name.size() ? name[i] : 0;
  }
  if (!reported.exchange(true)) REXLOG_INFO("Gamertag override: XamUserGetName {}", apply ? "applied" : "not applied");
}

REX_HOOK_RAW(__imp__XamUserGetSigninInfo) {
  static auto* original = Original("__imp__XamUserGetSigninInfo");
  static std::atomic<bool> reported{false};
  const auto user = ctx.r3.u32, info = ctx.r5.u32;
  original(ctx, base);
  const auto name = DisplayName();
  const bool apply = !name.empty() && user == 0 && ctx.r3.u32 == 0 && info;
  if (apply) {
    for (std::uint32_t i = 0; i < 16; ++i) base[info + 24 + i] = i < name.size() ? name[i] : 0;
  }
  if (!reported.exchange(true)) REXLOG_INFO("Gamertag override: XamUserGetSigninInfo {}", apply ? "applied" : "not applied");
}

REX_HOOK_RAW(__imp__XamUserGetDeviceContext) {
  static auto* original = Original("__imp__XamUserGetDeviceContext");
  const auto port = static_cast<std::uint32_t>(REXCVAR_GET(virtual_microphone_port));
  if (REXCVAR_GET(virtual_microphone) && ctx.r4.u32 == 4 && ctx.r3.u32 == port && ctx.r5.u32) {
    Wr32(base, ctx.r5.u32, kContextMic | port);
    ctx.r3.u64 = 0;
    return;
  }
  original(ctx, base);
}

REX_HOOK_RAW(__imp__MicDeviceRequest) {
  static auto* original = Original("__imp__MicDeviceRequest");
  const auto request = ctx.r3.u32;
  if (REXCVAR_GET(virtual_microphone)) {
    if (!g_scratch) g_scratch = rex::system::kernel_state()->memory()->SystemHeapAlloc(64);
    DrainReads(base);
    ctx.r3.u64 = EmulateMicDeviceRequest(base, request);
    return;
  }
  const auto before = REXCVAR_GET(trace_mic_device_requests) ? Snapshot(base, request) : std::string{};
  original(ctx, base);
  if (!REXCVAR_GET(trace_mic_device_requests)) return;
  static std::atomic_uint logged{};
  if (logged.fetch_add(1, std::memory_order_relaxed) >= 128) return;
  REXLOG_INFO("MIC request: address={:08X}, result={:08X}, before={}, after={}",
              request, ctx.r3.u32, before, Snapshot(base, request));
}
