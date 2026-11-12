#include "windows_ac3_output.h"
#include "ac3_encoder.h"
#include "iec61937.h"
#include "windows_audio_com.h"
#include <windows.h>
#include <audioclient.h>
#include <mmdeviceapi.h>
#include <mmreg.h>
#include <ksmedia.h>
#include <functiondiscoverykeys_devpkey.h>
#include <propvarutil.h>
#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <cstdint>
#include <exception>
#include <cstdio>
#include <deque>
#include <mutex>
#include <thread>
#include <utility>
#include <vector>
#include <rex/thread.h>
#include <rex/logging.h>

namespace {
constexpr unsigned kChannels = 6;
constexpr unsigned kInputSamples = 256;
constexpr unsigned kAc3Samples = 1536;
constexpr unsigned kCarrierBytes = rex::audio::kIec61937Ac3BurstBytes;
constexpr double kRearUpmixThreshold = 0.05;
constexpr float kRearUpmixDirectGain = 0.5f;
constexpr float kRearUpmixCrossGain = 0.25f;
constexpr float kCenterFoldGain = 0.70710678f;
using InputBlock = std::array<float, kInputSamples * kChannels>;
std::string HResultText(HRESULT hr) {
  char text[16]{};
  sprintf_s(text, "%08X", static_cast<unsigned>(hr));
  return text;
}
std::string EndpointName(IMMDevice* device) {
  IPropertyStore* properties = nullptr;
  HRESULT hr = device->OpenPropertyStore(STGM_READ, &properties);
  if (FAILED(hr)) return "unavailable (" + HResultText(hr) + ")";
  PROPVARIANT name;
  PropVariantInit(&name);
  hr = properties->GetValue(PKEY_Device_FriendlyName, &name);
  properties->Release();
  if (FAILED(hr)) {
    PropVariantClear(&name);
    return "unavailable (" + HResultText(hr) + ")";
  }
  std::string result = "(unnamed)";
  if (name.vt == VT_LPWSTR && name.pwszVal) {
    const int size = WideCharToMultiByte(CP_UTF8, 0, name.pwszVal, -1, nullptr, 0, nullptr, nullptr);
    if (size > 1) {
      std::vector<char> utf8(size);
      if (WideCharToMultiByte(CP_UTF8, 0, name.pwszVal, -1, utf8.data(), size, nullptr, nullptr))
        result = utf8.data();
      else
        result = "unavailable (" + HResultText(HRESULT_FROM_WIN32(GetLastError())) + ")";
    }
  }
  PropVariantClear(&name);
  return result;
}
}

rex::audio::Ac3RearUpmixResult rex::audio::ApplyAc3RearUpmix(
    float* interleaved, std::size_t frames) {
  if (!interleaved || !frames) return {false, 0.0f, 0.0f};

  double front_sum_squares = 0.0;
  double rear_sum_squares = 0.0;
  for (std::size_t sample = 0; sample < frames; ++sample) {
    const float* channels = interleaved + sample * kChannels;
    front_sum_squares += double(channels[0]) * channels[0] +
        double(channels[1]) * channels[1] +
        double(channels[2]) * channels[2];
    rear_sum_squares += double(channels[4]) * channels[4] +
        double(channels[5]) * channels[5];
  }

  const double denominator = static_cast<double>(frames) * 3.0;
  const float front_rms = static_cast<float>(
      std::sqrt(front_sum_squares / denominator));
  const float native_rear_rms = static_cast<float>(
      std::sqrt(rear_sum_squares / denominator));
  if (front_rms <= 0.0001f ||
      native_rear_rms >= front_rms * kRearUpmixThreshold) {
    return {false, front_rms, native_rear_rms};
  }

  for (std::size_t sample = 0; sample < frames; ++sample) {
    float* channels = interleaved + sample * kChannels;
    const float center = channels[2] * kCenterFoldGain;
    const float left = channels[0] + center;
    const float right = channels[1] + center;
    channels[4] = std::clamp(channels[4] * kRearUpmixDirectGain +
        left * kRearUpmixDirectGain - right * kRearUpmixCrossGain, -1.0f, 1.0f);
    channels[5] = std::clamp(channels[5] * kRearUpmixDirectGain +
        right * kRearUpmixDirectGain - left * kRearUpmixCrossGain, -1.0f, 1.0f);
  }
  return {true, front_rms, native_rear_rms};
}

struct WindowsAc3Output::Impl {
  explicit Impl(rex::thread::Semaphore* semaphore, bool use_event_callback)
      : semaphore(semaphore), use_event_callback(use_event_callback) {}
  rex::thread::Semaphore* semaphore;
  bool use_event_callback;
  std::mutex input_mutex;
  std::deque<InputBlock> input;
  std::deque<std::vector<std::uint8_t>> encoded;
  std::array<float, kAc3Samples * kChannels> encoder_input{};
  unsigned accumulated_samples = 0;
  std::vector<std::uint8_t> silence_burst;
  std::vector<std::uint8_t> render_burst;
  unsigned render_burst_offset = 0;
  bool render_burst_is_audio = false;
  std::atomic<bool> healthy{false};
  std::mutex startup_mutex;
  std::string startup_error;
  bool startup_ok = false;
  HANDLE input_event = nullptr;
  HANDLE stop_event = nullptr;
  HANDLE initialized_event = nullptr;
  HANDLE audio_event = nullptr;
  std::thread worker;
  WindowsAudioCom com;
  Ac3Encoder encoder;
  IMMDeviceEnumerator* enumerator = nullptr;
  IMMDevice* device = nullptr;
  IAudioClient* client = nullptr;
  IAudioRenderClient* render = nullptr;
  UINT32 buffer_frames = 0;
  unsigned underrun_count = 0;
  unsigned encoded_packet_count = 0;
  unsigned reported_audio_bursts = 0;
  bool reported_rear_upmix = false;
  unsigned reported_padding_polls = 0;
  bool reported_poll_fallback = false;
  bool started = false;
};

WindowsAc3Output::WindowsAc3Output(rex::thread::Semaphore* semaphore,
                                   bool use_event_callback)
    : impl_(new Impl(semaphore, use_event_callback)) {}

WindowsAc3Output::~WindowsAc3Output() {
  if (!impl_) return;
  SetEvent(impl_->stop_event);
  if (impl_->worker.joinable()) impl_->worker.join();
  if (impl_->audio_event) CloseHandle(impl_->audio_event);
  if (impl_->initialized_event) CloseHandle(impl_->initialized_event);
  if (impl_->stop_event) CloseHandle(impl_->stop_event);
  if (impl_->input_event) CloseHandle(impl_->input_event);
  delete impl_;
}

bool WindowsAc3Output::Initialize(std::string& error) {
  impl_->input_event = CreateEventW(nullptr, FALSE, FALSE, nullptr);
  impl_->stop_event = CreateEventW(nullptr, TRUE, FALSE, nullptr);
  impl_->initialized_event = CreateEventW(nullptr, TRUE, FALSE, nullptr);
  if (!impl_->input_event || !impl_->stop_event || !impl_->initialized_event) {
    error = "Could not create Dolby audio synchronization events: " + HResultText(HRESULT_FROM_WIN32(GetLastError()));
    return false;
  }
  impl_->worker = std::thread([this] { Run(); });
  if (WaitForSingleObject(impl_->initialized_event, INFINITE) != WAIT_OBJECT_0) {
    error = "Waiting for Dolby Digital output initialization failed";
    return false;
  }
  std::lock_guard lock(impl_->startup_mutex);
  error = impl_->startup_error;
  return impl_->startup_ok;
}

bool WindowsAc3Output::Submit(const float* samples) {
  if (!impl_ || !impl_->healthy.load(std::memory_order_acquire) || !samples) return false;
  {
    std::lock_guard lock(impl_->input_mutex);
    if (impl_->input.size() >= 64) return false;
    InputBlock block;
    std::copy_n(samples, block.size(), block.begin());
    impl_->input.push_back(std::move(block));
  }
  return SetEvent(impl_->input_event) != FALSE;
}

void WindowsAc3Output::SetStartupResult(bool success, std::string error) {
  {
    std::lock_guard lock(impl_->startup_mutex);
    impl_->startup_ok = success;
    impl_->startup_error = std::move(error);
  }
  impl_->healthy.store(success, std::memory_order_release);
  SetEvent(impl_->initialized_event);
}

void WindowsAc3Output::Fail(std::string error) noexcept {
  impl_->healthy.store(false, std::memory_order_release);
  REXLOG_ERROR("Dolby Digital output failed: {}", error);
  SetEvent(impl_->stop_event);
}

bool WindowsAc3Output::InitializeDevice(std::string& error) {
  HRESULT hr = impl_->com.Initialize();
  if (FAILED(hr)) { error = "COM MTA setup failed: " + HResultText(hr); return false; }
  hr = CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_INPROC_SERVER,
      __uuidof(IMMDeviceEnumerator), reinterpret_cast<void**>(&impl_->enumerator));
  if (FAILED(hr)) { error = "Audio endpoint enumeration failed: " + HResultText(hr); return false; }
  hr = impl_->enumerator->GetDefaultAudioEndpoint(eRender, eMultimedia, &impl_->device);
  if (FAILED(hr)) { error = "No default multimedia playback endpoint: " + HResultText(hr); return false; }
  REXLOG_INFO("Dolby Digital uses the default multimedia endpoint: {}", EndpointName(impl_->device));
  hr = impl_->device->Activate(__uuidof(IAudioClient), CLSCTX_INPROC_SERVER, nullptr,
      reinterpret_cast<void**>(&impl_->client));
  if (FAILED(hr)) { error = "Could not activate the default audio endpoint: " + HResultText(hr); return false; }
  WAVEFORMATEXTENSIBLE format{};
  format.Format.wFormatTag = WAVE_FORMAT_EXTENSIBLE;
  format.Format.nChannels = 2;
  format.Format.nSamplesPerSec = 48000;
  format.Format.nAvgBytesPerSec = 192000;
  format.Format.nBlockAlign = 4;
  format.Format.wBitsPerSample = 16;
  format.Format.cbSize = sizeof(format) - sizeof(format.Format);
  format.Samples.wValidBitsPerSample = 16;
  format.dwChannelMask = SPEAKER_FRONT_LEFT | SPEAKER_FRONT_RIGHT;
  format.SubFormat = KSDATAFORMAT_SUBTYPE_IEC61937_DOLBY_DIGITAL;
  hr = impl_->client->IsFormatSupported(AUDCLNT_SHAREMODE_EXCLUSIVE, &format.Format, nullptr);
  if (hr != S_OK) {
    error = "The default Windows playback endpoint does not accept exclusive 48 kHz Dolby Digital (AC-3): " + HResultText(hr);
    return false;
  }
  impl_->audio_event = CreateEventW(nullptr, FALSE, FALSE, nullptr);
  if (!impl_->audio_event) { error = "Could not create the Dolby audio event: " + HResultText(HRESULT_FROM_WIN32(GetLastError())); return false; }
  REFERENCE_TIME period = 2000000;
  const DWORD stream_flags = AUDCLNT_STREAMFLAGS_NOPERSIST |
      (impl_->use_event_callback ? AUDCLNT_STREAMFLAGS_EVENTCALLBACK : 0);
  const REFERENCE_TIME periodicity = impl_->use_event_callback ? period : 0;
  hr = impl_->client->Initialize(AUDCLNT_SHAREMODE_EXCLUSIVE,
      stream_flags, period, periodicity, &format.Format, nullptr);
  if (hr == AUDCLNT_E_BUFFER_SIZE_NOT_ALIGNED) {
    UINT32 aligned_frames = 0;
    hr = impl_->client->GetBufferSize(&aligned_frames);
    if (FAILED(hr)) { error = "Could not query the default endpoint's aligned Dolby buffer size: " + HResultText(hr); return false; }
    if (aligned_frames < kAc3Samples) {
      error = "The default endpoint's aligned buffer cannot hold a 1536-sample AC-3 burst";
      return false;
    }
    period = static_cast<REFERENCE_TIME>(
        (static_cast<std::uint64_t>(aligned_frames) * 10000000) /
        format.Format.nSamplesPerSec);
    hr = impl_->client->Initialize(AUDCLNT_SHAREMODE_EXCLUSIVE,
        stream_flags, period,
        impl_->use_event_callback ? period : 0, &format.Format, nullptr);
  }
  if (hr == AUDCLNT_E_DEVICE_IN_USE) {
    error = "The playback device is in use by another application; close other audio apps and restart to use Dolby Digital";
    return false;
  }
  if (FAILED(hr)) { error = "Exclusive Dolby Digital stream initialization failed: " + HResultText(hr); return false; }
  if (impl_->use_event_callback) {
    hr = impl_->client->SetEventHandle(impl_->audio_event);
    if (FAILED(hr)) { error = "Could not set the Dolby render event: " + HResultText(hr); return false; }
  }
  hr = impl_->client->GetBufferSize(&impl_->buffer_frames);
  if (FAILED(hr)) { error = "Could not query the Dolby endpoint buffer: " + HResultText(hr); return false; }
  REXLOG_INFO("Dolby Digital endpoint buffer initialized with {} frames", impl_->buffer_frames);
  if (impl_->buffer_frames < kAc3Samples) {
    error = "The HDMI endpoint buffer (" + std::to_string(impl_->buffer_frames) +
        " frames) cannot hold a 1536-sample AC-3 burst";
    return false;
  }
  REXLOG_INFO("Dolby Digital endpoint buffer: {} frames ({} AC-3 bursts)",
      impl_->buffer_frames, impl_->buffer_frames / kAc3Samples);
  hr = impl_->client->GetService(__uuidof(IAudioRenderClient),
      reinterpret_cast<void**>(&impl_->render));
  if (FAILED(hr)) { error = "Could not acquire the Dolby render client: " + HResultText(hr); return false; }
  return true;
}

void WindowsAc3Output::ProcessInput() {
  for (;;) {
    InputBlock block;
    {
      std::lock_guard lock(impl_->input_mutex);
      if (impl_->input.empty()) return;
      block = std::move(impl_->input.front());
      impl_->input.pop_front();
    }
    const unsigned remaining = kAc3Samples - impl_->accumulated_samples;
    const unsigned copied = std::min(remaining, kInputSamples);
    std::copy_n(block.data(), copied * kChannels,
        impl_->encoder_input.data() + impl_->accumulated_samples * kChannels);
    impl_->accumulated_samples += copied;
    if (copied < kInputSamples) {
      const unsigned rest = kInputSamples - copied;
      std::copy_n(block.data() + copied * kChannels, rest * kChannels,
          impl_->encoder_input.data() + impl_->accumulated_samples * kChannels);
      impl_->accumulated_samples += rest;
    }
    if (impl_->accumulated_samples == kAc3Samples) {
      const auto rear_upmix =
          rex::audio::ApplyAc3RearUpmix(impl_->encoder_input.data(), kAc3Samples);
      if (rear_upmix.applied && !impl_->reported_rear_upmix) {
        REXLOG_INFO("Dolby Digital rear ambience upmix applied: front RMS={:.5f}, native rear RMS={:.5f}",
            rear_upmix.front_rms, rear_upmix.native_rear_rms);
        impl_->reported_rear_upmix = true;
      }
      std::vector<std::uint8_t> packet;
      std::string error;
      if (!impl_->encoder.Encode(impl_->encoder_input.data(), packet, error)) {
        Fail(error);
        return;
      }
      if (!packet.empty()) {
        auto burst = rex::audio::MakeIec61937Ac3Burst(packet);
        if (burst.empty()) { Fail("The AC-3 encoder returned an invalid IEC 61937 frame"); return; }
        const unsigned packet_number = ++impl_->encoded_packet_count;
        if (packet_number <= 3 || packet_number % 128 == 0) {
          std::array<double, kChannels> sum_squares{};
          for (unsigned sample = 0; sample < kAc3Samples; ++sample) {
            for (unsigned channel = 0; channel < kChannels; ++channel) {
              const double value =
                  impl_->encoder_input[sample * kChannels + channel];
              sum_squares[channel] += value * value;
            }
          }
          const double denominator = static_cast<double>(kAc3Samples);
          REXLOG_INFO("Dolby Digital PCM input RMS: L={:.5f} R={:.5f} C={:.5f} LFE={:.5f} SL={:.5f} SR={:.5f} packet={} bytes={}",
              std::sqrt(sum_squares[0] / denominator),
              std::sqrt(sum_squares[1] / denominator),
              std::sqrt(sum_squares[2] / denominator),
              std::sqrt(sum_squares[3] / denominator),
              std::sqrt(sum_squares[4] / denominator),
              std::sqrt(sum_squares[5] / denominator),
              packet_number, packet.size());
        }
        impl_->encoded.push_back(std::move(burst));
      }
      impl_->accumulated_samples = 0;
    }
  }
}

bool WindowsAc3Output::RenderBuffer(bool initial, std::string& error) {
  UINT32 padding = 0;
  if (!initial) {
    const HRESULT hr = impl_->client->GetCurrentPadding(&padding);
    if (FAILED(hr)) { error = "Dolby endpoint padding query failed: " + HResultText(hr); return false; }
    if (impl_->reported_padding_polls < 3) {
      REXLOG_INFO("Dolby Digital endpoint padding: queued={} capacity={} available={}",
          padding, impl_->buffer_frames, impl_->buffer_frames - padding);
      ++impl_->reported_padding_polls;
    }
  }
  const UINT32 available_frames = impl_->buffer_frames - padding;
  if (!available_frames) return true;
  BYTE* destination = nullptr;
  HRESULT hr = impl_->render->GetBuffer(available_frames, &destination);
  if (FAILED(hr)) {
    error = "Dolby endpoint buffer acquisition failed: " + HResultText(hr);
    return false;
  }
  auto* output = reinterpret_cast<std::uint8_t*>(destination);
  unsigned consumed_frames = 0;
  UINT32 written_frames = 0;
  while (written_frames < available_frames) {
    if (impl_->render_burst.empty()) {
      if (!impl_->encoded.empty()) {
        impl_->render_burst = std::move(impl_->encoded.front());
        impl_->encoded.pop_front();
        impl_->render_burst_is_audio = true;
      } else {
        impl_->render_burst = impl_->silence_burst;
        impl_->render_burst_is_audio = false;
        if (!initial) {
          ++impl_->underrun_count;
          if (impl_->underrun_count == 1 || impl_->underrun_count % 100 == 0)
            REXLOG_WARN("Dolby Digital output underrun: sent silence burst (total={})",
                impl_->underrun_count);
        }
      }
      impl_->render_burst_offset = 0;
    }
    const unsigned burst_remaining = kAc3Samples - impl_->render_burst_offset;
    const unsigned copied_frames = std::min<unsigned>(
        burst_remaining, available_frames - written_frames);
    const unsigned burst_byte_offset =
        impl_->render_burst_offset * (kCarrierBytes / kAc3Samples);
    const unsigned copied_bytes = copied_frames * (kCarrierBytes / kAc3Samples);
    std::copy_n(impl_->render_burst.data() + burst_byte_offset, copied_bytes,
        output + written_frames * (kCarrierBytes / kAc3Samples));
    impl_->render_burst_offset += copied_frames;
    written_frames += copied_frames;
    if (impl_->render_burst_offset == kAc3Samples) {
      if (impl_->render_burst_is_audio && !initial)
        consumed_frames += kAc3Samples / kInputSamples;
      impl_->render_burst.clear();
      impl_->render_burst_offset = 0;
      impl_->render_burst_is_audio = false;
    }
  }
  hr = impl_->render->ReleaseBuffer(available_frames, 0);
  if (FAILED(hr)) {
    error = "Dolby endpoint buffer submission failed: " + HResultText(hr);
    return false;
  }
  if (consumed_frames && impl_->semaphore &&
      !impl_->semaphore->Release(consumed_frames, nullptr)) {
    error = "Could not signal consumed Dolby audio frames to the game audio worker";
    return false;
  }
  if (consumed_frames) {
    const unsigned bursts = consumed_frames / (kAc3Samples / kInputSamples);
    impl_->reported_audio_bursts += bursts;
    if (impl_->reported_audio_bursts <= 3 ||
        impl_->reported_audio_bursts % 128 < bursts) {
      REXLOG_INFO("Dolby Digital submitted AC-3 audio: total_bursts={} guest_frames={}",
          impl_->reported_audio_bursts, consumed_frames);
    }
  }
  if (initial) {
    const HRESULT hr = impl_->client->Start();
    if (FAILED(hr)) { error = "Dolby endpoint could not start: " + HResultText(hr); return false; }
    impl_->started = true;
  }
  return true;
}

void WindowsAc3Output::Run() noexcept {
  const char* stage = "FFmpeg AC-3 encoder";
  std::string error;
  const auto cleanup = [this] {
    if (impl_->client && impl_->started) impl_->client->Stop();
    if (impl_->render) { impl_->render->Release(); impl_->render = nullptr; }
    if (impl_->client) { impl_->client->Release(); impl_->client = nullptr; }
    if (impl_->device) { impl_->device->Release(); impl_->device = nullptr; }
    if (impl_->enumerator) { impl_->enumerator->Release(); impl_->enumerator = nullptr; }
  };
  try {
    const auto initialize = [this, &stage, &error] {
      if (!impl_->encoder.Initialize(error)) return false;
      stage = "WASAPI exclusive AC-3 endpoint";
      if (!InitializeDevice(error)) return false;
      std::array<float, kAc3Samples * kChannels> silence{};
      for (unsigned i = 0; i < 4 && impl_->silence_burst.empty(); ++i) {
        std::vector<std::uint8_t> packet;
        if (!impl_->encoder.Encode(silence.data(), packet, error)) return false;
        if (!packet.empty())
          impl_->silence_burst = rex::audio::MakeIec61937Ac3Burst(packet);
      }
      if (impl_->silence_burst.empty()) {
        error = "The AC-3 encoder did not produce its initial silent frame";
        return false;
      }
      return RenderBuffer(true, error);
    };
    if (!initialize()) { SetStartupResult(false, error); cleanup(); return; }
    REXLOG_INFO("Dolby Digital 5.1 active: FFmpeg AC-3 at 640 kb/s, 48 kHz, WASAPI exclusive IEC 61937 output");
    SetStartupResult(true, {});
    HANDLE events[] = {impl_->stop_event, impl_->input_event, impl_->audio_event};
    for (;;) {
      const DWORD result = WaitForMultipleObjects(3, events, FALSE, 10);
      if (result == WAIT_OBJECT_0) break;
      if (result == WAIT_FAILED) { Fail("Dolby output event wait failed: " + HResultText(HRESULT_FROM_WIN32(GetLastError()))); break; }
      if (WaitForSingleObject(impl_->stop_event, 0) == WAIT_OBJECT_0) break;
      if (result == WAIT_TIMEOUT && !impl_->reported_poll_fallback) {
        REXLOG_WARN("Dolby Digital {} output is using 10 ms endpoint-padding polling",
            impl_->use_event_callback ? "event fallback" : "timer-driven");
        impl_->reported_poll_fallback = true;
      }
      ProcessInput();
      if (!impl_->healthy.load(std::memory_order_acquire)) break;
      if (!RenderBuffer(false, error)) { Fail(error); break; }
    }
  } catch (const std::exception& exception) {
    error = std::string(stage) + ": " + exception.what();
    if (WaitForSingleObject(impl_->initialized_event, 0) == WAIT_OBJECT_0) Fail(error);
    else SetStartupResult(false, error);
  } catch (...) {
    error = std::string(stage) + ": unknown error";
    if (WaitForSingleObject(impl_->initialized_event, 0) == WAIT_OBJECT_0) Fail(error);
    else SetStartupResult(false, error);
  }
  cleanup();
}
