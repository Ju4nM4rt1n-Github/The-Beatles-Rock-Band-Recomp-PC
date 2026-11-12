// Exercise the production driver, not a duplicate initialization sequence.
#include <windows.h>
#include <mmsystem.h>
#include <xaudio2.h>
#include <audioclient.h>
#include <mfapi.h>
#include <mfidl.h>
#include <mftransform.h>
#include <mmdeviceapi.h>
#include <functiondiscoverykeys_devpkey.h>
#include <propvarutil.h>
#include <ksmedia.h>
#include <array>
#include <atomic>
#include <chrono>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <cstdio>
#include <fstream>
#include <memory>
#include <string_view>
#include <stdexcept>
#include <thread>
#include <vector>
extern "C" {
#include <libavcodec/avcodec.h>
#include <libavutil/samplefmt.h>
}
#include "../src/ac3_encoder.h"
#include "../src/iec61937.h"
#include "../src/windows_ac3_output.h"

static std::atomic<bool> fail_engine{false};
static HRESULT TestXAudio2Create(IXAudio2** engine, UINT32 flags, XAUDIO2_PROCESSOR processor) {
  if (fail_engine.load()) { *engine = nullptr; return E_FAIL; }
  return XAudio2Create(engine, flags, processor);
}
#define XAudio2Create TestXAudio2Create
#include "../src/windows_audio.cpp"
#undef XAudio2Create
REXCVAR_DEFINE_BOOL(trace_audio_frames, false, "Tests", "No performance tracing");
REXCVAR_DEFINE_STRING(user_display_name, "", "Tests", "Test the player-name command-line override");

static int checks = 0;
static void Check(bool ok, const char* label) {
  if (!ok) throw std::runtime_error(label);
  ++checks; std::printf("PASS %s\n", label);
}
static double SampleValue(const AVFrame* frame, int channel, int sample) {
  const auto format = static_cast<AVSampleFormat>(frame->format);
  const auto packed_format = av_get_packed_sample_fmt(format);
  const bool planar = av_sample_fmt_is_planar(format) != 0;
  const int bytes_per_sample = av_get_bytes_per_sample(format);
  const auto* data = frame->extended_data[planar ? channel : 0] +
      (planar ? sample : sample * frame->ch_layout.nb_channels + channel) * bytes_per_sample;
  switch (packed_format) {
    case AV_SAMPLE_FMT_FLT: {
      float value;
      std::memcpy(&value, data, sizeof(value));
      return value;
    }
    case AV_SAMPLE_FMT_DBL: {
      double value;
      std::memcpy(&value, data, sizeof(value));
      return value;
    }
    case AV_SAMPLE_FMT_S16: {
      std::int16_t value;
      std::memcpy(&value, data, sizeof(value));
      return value / 32768.0;
    }
    case AV_SAMPLE_FMT_S32: {
      std::int32_t value;
      std::memcpy(&value, data, sizeof(value));
      return value / 2147483648.0;
    }
    case AV_SAMPLE_FMT_U8:
      return (static_cast<int>(*data) - 128) / 128.0;
    default:
      return 0.0;
  }
}
static bool DecodeAc3Packet(AVCodecContext* decoder, AVPacket* packet, AVFrame* frame,
                            std::array<double, 6>& sum_squares,
                            std::array<std::uint64_t, 6>& sample_counts) {
  int result = avcodec_send_packet(decoder, packet);
  if (result < 0) return false;
  while ((result = avcodec_receive_frame(decoder, frame)) >= 0) {
    if (frame->ch_layout.nb_channels != 6) return false;
    for (int sample = 0; sample < frame->nb_samples; ++sample) {
      for (int channel = 0; channel < frame->ch_layout.nb_channels; ++channel) {
        const double value = SampleValue(frame, channel, sample);
        sum_squares[channel] += value * value;
        ++sample_counts[channel];
      }
    }
    av_frame_unref(frame);
  }
  return result == AVERROR(EAGAIN) || result == AVERROR_EOF;
}
static bool Ac3ToneRoundTrip(std::array<double, 6>& rms) {
  const AVCodec* decoder_codec = avcodec_find_decoder(AV_CODEC_ID_AC3);
  if (!decoder_codec) return false;
  AVCodecContext* decoder = avcodec_alloc_context3(decoder_codec);
  AVPacket* packet = av_packet_alloc();
  AVFrame* frame = av_frame_alloc();
  if (!decoder || !packet || !frame) {
    avcodec_free_context(&decoder);
    av_packet_free(&packet);
    av_frame_free(&frame);
    return false;
  }
  int result = avcodec_open2(decoder, decoder_codec, nullptr);
  if (result < 0) {
    avcodec_free_context(&decoder);
    av_packet_free(&packet);
    av_frame_free(&frame);
    return false;
  }

  Ac3Encoder encoder;
  std::string error;
  bool success = encoder.Initialize(error);
  std::array<double, 6> sum_squares{};
  std::array<std::uint64_t, 6> sample_counts{};
  std::array<float, 1536 * 6> input{};
  constexpr double kPi = 3.14159265358979323846;
  constexpr std::array<double, 6> frequencies = {440.0, 440.0, 440.0, 100.0, 440.0, 440.0};
  for (int frame_index = 0; success && frame_index < 8; ++frame_index) {
    const int channel = frame_index % 6;
    for (int sample = 0; sample < 1536; ++sample) {
      input[sample * 6 + channel] = 0.2f * static_cast<float>(
          std::sin(2.0 * kPi * frequencies[channel] * sample / 48000.0));
    }
    std::vector<std::uint8_t> encoded;
    success = encoder.Encode(input.data(), encoded, error);
    if (!success || encoded.empty()) continue;
    const auto burst = rex::audio::MakeIec61937Ac3Burst(encoded);
    if (burst.size() != rex::audio::kIec61937Ac3BurstBytes ||
        burst[0] != 0x72 || burst[1] != 0xF8 ||
        burst[2] != 0x1F || burst[3] != 0x4E ||
        (static_cast<unsigned>(burst[4]) |
         (static_cast<unsigned>(burst[5]) << 8)) !=
            (0x0001 | ((encoded[5] & 0x07) << 8)) ||
        (static_cast<unsigned>(burst[6]) |
         (static_cast<unsigned>(burst[7]) << 8)) != encoded.size() * 8) {
      success = false;
      break;
    }
    std::vector<std::uint8_t> restored(encoded.size());
    for (size_t i = 0; i < restored.size(); i += 2) {
      restored[i] = burst[8 + i + 1];
      restored[i + 1] = burst[8 + i];
    }
    if (restored != encoded) {
      success = false;
      break;
    }
    result = av_new_packet(packet, static_cast<int>(restored.size()));
    if (result < 0) { success = false; break; }
    std::memcpy(packet->data, restored.data(), restored.size());
    success = DecodeAc3Packet(decoder, packet, frame, sum_squares, sample_counts);
    av_packet_unref(packet);
  }
  for (size_t channel = 0; channel < rms.size(); ++channel) {
    if (!sample_counts[channel]) success = false;
    else rms[channel] = std::sqrt(sum_squares[channel] / sample_counts[channel]);
  }
  avcodec_free_context(&decoder);
  av_packet_free(&packet);
  av_frame_free(&frame);
  return success;
}
static bool WriteAc3ToneFile() {
  Ac3Encoder encoder;
  std::string error;
  if (!encoder.Initialize(error)) {
    std::printf("AC-3 test file creation failed: %s\n", error.c_str());
    return false;
  }
  std::ofstream file("iec61937-tone-test.ac3", std::ios::binary);
  if (!file) {
    std::printf("Could not create iec61937-tone-test.ac3\n");
    return false;
  }
  constexpr unsigned kFramesPerChannel = 32;
  constexpr unsigned kFramesPerTone = 24;
  constexpr unsigned kChannelCount = 6;
  constexpr double frequencies[kChannelCount] = {440.0, 440.0, 440.0, 100.0, 440.0, 440.0};
  constexpr double kPi = 3.14159265358979323846;
  std::array<float, 1536 * kChannelCount> input{};
  unsigned packet_count = 0;
  for (unsigned frame_index = 0; frame_index < kFramesPerChannel * kChannelCount + 4;
       ++frame_index) {
    input.fill(0.0f);
    if (frame_index < kFramesPerChannel * kChannelCount) {
      const unsigned channel = frame_index / kFramesPerChannel;
      const unsigned frame_in_segment = frame_index % kFramesPerChannel;
      if (frame_in_segment < kFramesPerTone) {
        for (unsigned sample = 0; sample < 1536; ++sample) {
          const double position = frame_in_segment * 1536 + sample;
          input[sample * kChannelCount + channel] = 0.2f * static_cast<float>(
              std::sin(2.0 * kPi * frequencies[channel] * position / 48000.0));
        }
      }
    }
    std::vector<std::uint8_t> packet;
    if (!encoder.Encode(input.data(), packet, error)) {
      std::printf("AC-3 test file encoding failed: %s\n", error.c_str());
      return false;
    }
    if (!packet.empty()) {
      file.write(reinterpret_cast<const char*>(packet.data()),
                 static_cast<std::streamsize>(packet.size()));
      if (!file) {
        std::printf("Could not write iec61937-tone-test.ac3\n");
        return false;
      }
      ++packet_count;
    }
  }
  file.close();
  if (!file || !packet_count) {
    std::printf("AC-3 test file did not finish writing valid packets\n");
    return false;
  }
  std::printf("Wrote iec61937-tone-test.ac3 with %u AC-3 packets\n", packet_count);
  return true;
}
static APTTYPE Apartment() {
  APTTYPE type; APTTYPEQUALIFIER qualifier;
  Check(SUCCEEDED(CoGetApartmentType(&type, &qualifier)), "COM apartment is available");
  return type;
}
static void ProbeAc3EndpointSupport() {
  IMMDeviceEnumerator* enumerator = nullptr;
  HRESULT hr = CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_INPROC_SERVER,
      __uuidof(IMMDeviceEnumerator), reinterpret_cast<void**>(&enumerator));
  if (FAILED(hr)) { std::printf("AC3 endpoint probe unavailable: enumerator error %08X\n", unsigned(hr)); return; }
  IMMDevice* default_device = nullptr;
  IPropertyStore* default_properties = nullptr;
  PROPVARIANT default_name;
  PropVariantInit(&default_name);
  if (SUCCEEDED(enumerator->GetDefaultAudioEndpoint(eRender, eMultimedia, &default_device)) &&
      SUCCEEDED(default_device->OpenPropertyStore(STGM_READ, &default_properties)) &&
      SUCCEEDED(default_properties->GetValue(PKEY_Device_FriendlyName, &default_name))) {
    std::printf("AC3 default multimedia endpoint: %ls\n",
        default_name.pwszVal ? default_name.pwszVal : L"(unnamed)");
  } else {
    std::printf("AC3 default multimedia endpoint: unavailable\n");
  }
  PropVariantClear(&default_name);
  if (default_properties) default_properties->Release();
  if (default_device) default_device->Release();
  IMMDeviceCollection* devices = nullptr;
  hr = enumerator->EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE, &devices);
  if (FAILED(hr)) { std::printf("AC3 endpoint probe unavailable: enumeration error %08X\n", unsigned(hr)); enumerator->Release(); return; }
  WAVEFORMATEXTENSIBLE ac3{};
  ac3.Format.wFormatTag = WAVE_FORMAT_EXTENSIBLE;
  ac3.Format.nChannels = 2;
  ac3.Format.nSamplesPerSec = 48000;
  ac3.Format.nAvgBytesPerSec = 192000;
  ac3.Format.nBlockAlign = 4;
  ac3.Format.wBitsPerSample = 16;
  ac3.Format.cbSize = sizeof(ac3) - sizeof(ac3.Format);
  ac3.Samples.wValidBitsPerSample = 16;
  ac3.dwChannelMask = SPEAKER_FRONT_LEFT | SPEAKER_FRONT_RIGHT;
  ac3.SubFormat = KSDATAFORMAT_SUBTYPE_IEC61937_DOLBY_DIGITAL;
  UINT count = 0;
  devices->GetCount(&count);
  for (UINT i = 0; i < count; ++i) {
    IMMDevice* device = nullptr;
    IPropertyStore* properties = nullptr;
    IAudioClient* client = nullptr;
    PROPVARIANT name;
    PropVariantInit(&name);
    if (SUCCEEDED(devices->Item(i, &device)) &&
        SUCCEEDED(device->OpenPropertyStore(STGM_READ, &properties)) &&
        SUCCEEDED(properties->GetValue(PKEY_Device_FriendlyName, &name)) &&
        SUCCEEDED(device->Activate(__uuidof(IAudioClient), CLSCTX_INPROC_SERVER, nullptr,
            reinterpret_cast<void**>(&client)))) {
      hr = client->IsFormatSupported(AUDCLNT_SHAREMODE_EXCLUSIVE, &ac3.Format, nullptr);
      std::printf("AC3 endpoint probe: %ls exclusive_format=%s (HRESULT %08X)\n",
          name.pwszVal ? name.pwszVal : L"(unnamed)", hr == S_OK ? "supported" : "not reported supported", unsigned(hr));
    }
    if (client) client->Release();
    if (properties) properties->Release();
    if (device) device->Release();
    PropVariantClear(&name);
  }
  devices->Release();
  enumerator->Release();
}
static void ProbeWindowsAc3Encoder() {
  HRESULT hr = MFStartup(MF_VERSION, MFSTARTUP_FULL);
  if (FAILED(hr)) { std::printf("Windows AC3 encoder probe unavailable: Media Foundation error %08X\n", unsigned(hr)); return; }
  MFT_REGISTER_TYPE_INFO output{MFMediaType_Audio, MFAudioFormat_Dolby_AC3};
  IMFActivate** activations = nullptr;
  UINT32 count = 0;
  hr = MFTEnumEx(MFT_CATEGORY_AUDIO_ENCODER,
      MFT_ENUM_FLAG_SYNCMFT | MFT_ENUM_FLAG_LOCALMFT | MFT_ENUM_FLAG_SORTANDFILTER,
      nullptr, &output, &activations, &count);
  if (SUCCEEDED(hr)) {
    for (UINT32 i = 0; i < count; ++i) {
      WCHAR name[256]{};
      UINT32 length = 0;
      if (SUCCEEDED(activations[i]->GetString(MFT_FRIENDLY_NAME_Attribute, name, 256, &length)))
        std::printf("Windows AC3 encoder: %ls\n", name);
      IMFTransform* transform = nullptr;
      if (SUCCEEDED(activations[i]->ActivateObject(IID_PPV_ARGS(&transform)))) {
        for (DWORD type_index = 0; ; ++type_index) {
          IMFMediaType* type = nullptr;
          if (FAILED(transform->GetInputAvailableType(0, type_index, &type))) break;
          GUID subtype{}; UINT32 channels = 0, rate = 0, bits = 0;
          type->GetGUID(MF_MT_SUBTYPE, &subtype);
          type->GetUINT32(MF_MT_AUDIO_NUM_CHANNELS, &channels);
          type->GetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, &rate);
          type->GetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, &bits);
          std::printf("  input type: subtype=%08X channels=%u rate=%u bits=%u\n",
              unsigned(subtype.Data1), channels, rate, bits);
          type->Release();
        }
        for (DWORD type_index = 0; ; ++type_index) {
          IMFMediaType* type = nullptr;
          if (FAILED(transform->GetOutputAvailableType(0, type_index, &type))) break;
          GUID subtype{}; UINT32 channels = 0, rate = 0, bits = 0, bitrate = 0;
          type->GetGUID(MF_MT_SUBTYPE, &subtype);
          type->GetUINT32(MF_MT_AUDIO_NUM_CHANNELS, &channels);
          type->GetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, &rate);
          type->GetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, &bits);
          type->GetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, &bitrate);
          std::printf("  output type: subtype=%08X channels=%u rate=%u bits=%u bytes_per_second=%u\n",
              unsigned(subtype.Data1), channels, rate, bits, bitrate);
          type->Release();
        }
        IMFMediaType* output_type = nullptr;
        if (SUCCEEDED(transform->GetOutputAvailableType(0, 0, &output_type)) &&
            SUCCEEDED(transform->SetOutputType(0, output_type, MFT_SET_TYPE_TEST_ONLY))) {
          for (const auto& candidate : {std::pair{MFAudioFormat_Float, 32u}, std::pair{MFAudioFormat_PCM, 16u},
                                        std::pair{MFAudioFormat_PCM, 32u}}) {
            IMFMediaType* input_type = nullptr;
            if (FAILED(MFCreateMediaType(&input_type))) continue;
            input_type->SetGUID(MF_MT_MAJOR_TYPE, MFMediaType_Audio);
            input_type->SetGUID(MF_MT_SUBTYPE, candidate.first);
            input_type->SetUINT32(MF_MT_AUDIO_SAMPLES_PER_SECOND, 48000);
            input_type->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, 6);
            input_type->SetUINT32(MF_MT_AUDIO_BITS_PER_SAMPLE, candidate.second);
            input_type->SetUINT32(MF_MT_AUDIO_BLOCK_ALIGNMENT, 6 * candidate.second / 8);
            input_type->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, 48000 * 6 * candidate.second / 8);
            input_type->SetUINT32(MF_MT_AUDIO_CHANNEL_MASK, 0x3F);
            hr = transform->SetInputType(0, input_type, MFT_SET_TYPE_TEST_ONLY);
            std::printf("  5.1 %s-bit input test: %s (HRESULT %08X)\n",
                candidate.first == MFAudioFormat_Float ? "float32" : (candidate.second == 16 ? "PCM16" : "PCM32"),
                SUCCEEDED(hr) ? "accepted" : "rejected", unsigned(hr));
            input_type->Release();
          }
          IMFMediaType* surround_output = nullptr;
          if (SUCCEEDED(MFCreateMediaType(&surround_output))) {
            output_type->CopyAllItems(surround_output);
            surround_output->SetUINT32(MF_MT_AUDIO_NUM_CHANNELS, 6);
            surround_output->SetUINT32(MF_MT_AUDIO_AVG_BYTES_PER_SECOND, 80000);
            const HRESULT surround_hr = transform->SetOutputType(0, surround_output, MFT_SET_TYPE_TEST_ONLY);
            std::printf("  5.1 AC3 output type test: %s (HRESULT %08X)\n",
                SUCCEEDED(surround_hr) ? "accepted" : "rejected", unsigned(surround_hr));
            surround_output->Release();
          }
        }
        if (output_type) output_type->Release();
        transform->Release();
      }
      activations[i]->Release();
    }
    CoTaskMemFree(activations);
    if (!count) std::printf("Windows AC3 encoder probe: no registered AC3 encoder MFT\n");
  } else {
    std::printf("Windows AC3 encoder probe failed: MFTEnumEx error %08X\n", unsigned(hr));
  }
  MFShutdown();
}
int main(int argc, char** argv) {
  if (argc == 2 && std::string_view(argv[1]) == "--write-ac3-test")
    return WriteAc3ToneFile() ? 0 : 1;
  SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX);
  if (argc == 2 && std::string_view(argv[1]) == "--probe-ac3-output") {
    WindowsAc3Output output(nullptr, false);
    std::string error;
    if (!output.Initialize(error)) {
      std::printf("AC-3 output probe failed: %s\n", error.c_str());
      return 1;
    }
    std::printf("AC-3 exclusive output probe active for 2 seconds\n");
    Sleep(2000);
    return 0;
  }
  if (argc == 2 && std::string_view(argv[1]) == "--probe-ac3-tone") {
    rex::InitLogging("windows-audio-ac3-tone-probe.log");
    WindowsAc3Output output(nullptr, false);
    std::string error;
    if (!output.Initialize(error)) {
      std::printf("AC-3 tone probe failed: %s\n", error.c_str());
      return 1;
    }

    constexpr unsigned kSampleRate = 48000;
    constexpr unsigned kBlockSamples = 256;
    constexpr unsigned kChannels = 6;
    constexpr unsigned kAc3Samples = 1536;
    constexpr unsigned kSegmentSamples = kAc3Samples * 32;
    constexpr unsigned kToneSamples = kSegmentSamples * 3 / 4;
    constexpr float kAmplitude = 0.2f;
    constexpr double kPi = 3.14159265358979323846;
    constexpr unsigned kSegmentCount = 6;
    constexpr double kFrequencies[kChannels] = {440.0, 440.0, 440.0, 100.0, 440.0, 440.0};
    constexpr const char* kChannelNames[kChannels] = {
        "front left", "front right", "center", "LFE", "surround left", "surround right"};

    const MMRESULT timer_result = timeBeginPeriod(1);
    std::array<float, kBlockSamples * kChannels> block{};
    const auto block_duration = std::chrono::duration<double>(double(kBlockSamples) / kSampleRate);
    auto next_block = std::chrono::steady_clock::now();
    const unsigned total_blocks = kSegmentSamples * kSegmentCount / kBlockSamples;
    unsigned announced_segment = kSegmentCount;
    std::printf("AC-3 5.1 tone probe: moderate-level, sequential channel test on the default multimedia endpoint.\n");
    for (unsigned block_index = 0; block_index < total_blocks; ++block_index) {
      const unsigned first_sample = block_index * kBlockSamples;
      const unsigned segment = first_sample / kSegmentSamples;
      if (segment != announced_segment) {
        announced_segment = segment;
        std::printf("Testing %s\n", kChannelNames[segment]);
      }
      block.fill(0.0f);
      for (unsigned sample = 0; sample < kBlockSamples; ++sample) {
        const unsigned in_segment = (first_sample + sample) % kSegmentSamples;
        if (in_segment >= kToneSamples) continue;
        const float tone = kAmplitude * static_cast<float>(
            std::sin(2.0 * kPi * kFrequencies[segment] * in_segment / kSampleRate));
        block[sample * kChannels + segment] = tone;
      }

      const auto submit_deadline = std::chrono::steady_clock::now() + std::chrono::seconds(2);
      while (!output.Submit(block.data())) {
        if (std::chrono::steady_clock::now() >= submit_deadline) {
          std::printf("AC-3 tone probe stopped: output queue did not accept audio.\n");
          if (timer_result == TIMERR_NOERROR) timeEndPeriod(1);
          return 1;
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(1));
      }

      next_block += std::chrono::duration_cast<std::chrono::steady_clock::duration>(block_duration);
      std::this_thread::sleep_until(next_block);
    }
    if (timer_result == TIMERR_NOERROR) timeEndPeriod(1);
    std::printf("Channel test sent; allowing the HDMI/ARC buffer to drain.\n");
    Sleep(1000);
    return 0;
  }
  _set_abort_behavior(0, _WRITE_ABORT_MSG | _CALL_REPORTFAULT);
  rex::cvar::Init(argc, argv);
  std::string expected_name;
  for (int i = 1; i < argc; ++i) {
    constexpr char name_argument[] = "--user_display_name=";
    if (std::string_view(argv[i]).starts_with(name_argument)) expected_name = argv[i] + sizeof(name_argument) - 1;
  }
  Check(kGuestAudioChannels == 6, "guest surround PCM keeps its six-channel layout");
  {
    Ac3Encoder encoder;
    std::string encoder_error;
    Check(encoder.Initialize(encoder_error), encoder_error.empty() ? "FFmpeg 5.1 AC-3 encoder initializes" : encoder_error.c_str());
    Check(encoder.FrameSize() == 1536, "AC-3 encoder uses one 1536-sample frame");
    const std::vector<float> silence(static_cast<size_t>(encoder.FrameSize()) * kGuestAudioChannels);
    std::vector<std::uint8_t> encoded;
    for (int i = 0; i < 4 && encoded.empty(); ++i)
      Check(encoder.Encode(silence.data(), encoded, encoder_error),
          encoder_error.empty() ? "silent 5.1 PCM frame encodes" : encoder_error.c_str());
    Check(encoded.size() >= 7 && encoded[0] == 0x0B && encoded[1] == 0x77,
        "encoder returns a valid AC-3 sync frame");
  }
  {
    constexpr size_t kFrames = 1536;
    std::array<float, kFrames * kGuestAudioChannels> pcm{};
    std::array<float, kFrames * kGuestAudioChannels> original{};
    constexpr double kPi = 3.14159265358979323846;
    for (size_t sample = 0; sample < kFrames; ++sample) {
      pcm[sample * kGuestAudioChannels] = 0.2f * static_cast<float>(
          std::sin(2.0 * kPi * 440.0 * sample / 48000.0));
      pcm[sample * kGuestAudioChannels + 1] = 0.15f * static_cast<float>(
          std::sin(2.0 * kPi * 660.0 * sample / 48000.0));
      pcm[sample * kGuestAudioChannels + 2] = 0.03f;
      pcm[sample * kGuestAudioChannels + 3] = 0.01f;
    }
    original = pcm;
    const auto upmix = rex::audio::ApplyAc3RearUpmix(pcm.data(), kFrames);
    Check(upmix.applied && upmix.front_rms > 0.01f &&
          upmix.native_rear_rms < upmix.front_rms * 0.05f,
        "near-silent native rear channels activate the Dolby rear upmix");
    double rear_sum_squares = 0.0;
    bool front_and_center_unchanged = true;
    for (size_t sample = 0; sample < kFrames; ++sample) {
      for (size_t channel = 0; channel < 4; ++channel)
        front_and_center_unchanged &= pcm[sample * kGuestAudioChannels + channel] ==
            original[sample * kGuestAudioChannels + channel];
      rear_sum_squares += double(pcm[sample * kGuestAudioChannels + 4]) *
          pcm[sample * kGuestAudioChannels + 4];
      rear_sum_squares += double(pcm[sample * kGuestAudioChannels + 5]) *
          pcm[sample * kGuestAudioChannels + 5];
    }
    Check(front_and_center_unchanged,
        "rear upmix preserves front, center, and LFE samples");
    const double rear_rms = std::sqrt(rear_sum_squares / (kFrames * 2));
    Check(rear_rms > 0.03,
        "rear upmix produces measurable signal from a stereo PCM frame");

    for (size_t sample = 0; sample < kFrames; ++sample) {
      pcm[sample * kGuestAudioChannels + 4] = original[sample * kGuestAudioChannels];
      pcm[sample * kGuestAudioChannels + 5] = original[sample * kGuestAudioChannels + 1];
    }
    const auto native_surround =
        rex::audio::ApplyAc3RearUpmix(pcm.data(), kFrames);
    Check(!native_surround.applied,
        "rear upmix leaves meaningful native surround channels unchanged");

    pcm.fill(0.0f);
    for (size_t sample = 0; sample < kFrames; ++sample) {
      pcm[sample * kGuestAudioChannels + 2] = 0.2f * static_cast<float>(
          std::sin(2.0 * kPi * 440.0 * sample / 48000.0));
    }
    const auto center_upmix =
        rex::audio::ApplyAc3RearUpmix(pcm.data(), kFrames);
    Check(center_upmix.applied,
        "center-only game audio also activates the rear ambience upmix");
    rear_sum_squares = 0.0;
    for (size_t sample = 0; sample < kFrames; ++sample) {
      rear_sum_squares += double(pcm[sample * kGuestAudioChannels + 4]) *
          pcm[sample * kGuestAudioChannels + 4];
      rear_sum_squares += double(pcm[sample * kGuestAudioChannels + 5]) *
          pcm[sample * kGuestAudioChannels + 5];
    }
    Check(std::sqrt(rear_sum_squares / (kFrames * 2)) > 0.01,
        "center channel is audible in the synthesized rear channels");
  }
  std::array<double, 6> ac3_rms{};
  Check(Ac3ToneRoundTrip(ac3_rms), "IEC 61937 burst unwraps and AC-3 packets decode locally");
  constexpr const char* channel_names[] = {
      "front left", "front right", "center", "LFE", "surround left", "surround right"};
  for (size_t channel = 0; channel < ac3_rms.size(); ++channel) {
    std::printf("Decoded IEC 61937 %s tone RMS: %.4f\n", channel_names[channel], ac3_rms[channel]);
    Check(ac3_rms[channel] > 0.01, "decoded IEC 61937 channel tone is not silent");
  }
  Check(REXCVAR_GET(user_display_name) == expected_name,
      "player name command-line override preserves spaces");
  if (argc == 2 && std::string(argv[1]) == "--fail-driver") {
    rex::InitLogging("windows-audio-test-failure.log");
    fail_engine = true;
    auto driver = CreateWindowsAudioDriver(nullptr, nullptr);
    return 99;  // Must stop at the audio error, never return a null guest handle.
  }
  try {
    IMMDeviceEnumerator* enumerator = nullptr;
    HRESULT before = CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr,
        CLSCTX_INPROC_SERVER, __uuidof(IMMDeviceEnumerator), reinterpret_cast<void**>(&enumerator));
    if (enumerator) enumerator->Release();
    Check(before == CO_E_NOTINITIALIZED, "fresh process reproduces missing COM prerequisite");
    {
      WindowsAudioDriver driver(nullptr, nullptr);
      Check(driver.Initialize(), "production XAudio2 driver starts without prior COM initialization");
      Check(Apartment() == APTTYPE_MTA, "audio owns a live implicit MTA");
    }
    if (SUCCEEDED(CoInitializeEx(nullptr, COINIT_MULTITHREADED))) {
      ProbeAc3EndpointSupport();
      ProbeWindowsAc3Encoder();
      CoUninitialize();
    }
    std::exception_ptr error;
    auto run = [&](auto fn) {
      std::thread worker([&] { try { fn(); } catch (...) { error = std::current_exception(); } });
      worker.join(); if (error) std::rethrow_exception(error);
    };
    run([] {
      Check(SUCCEEDED(CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED)), "existing STA setup");
      const auto caller_apartment = Apartment();
      Check(caller_apartment == APTTYPE_STA || caller_apartment == APTTYPE_MAINSTA, "caller is in an STA apartment");
      {
        WindowsAudioDriver driver(nullptr, nullptr);
        Check(driver.Initialize(), "production driver accepts an existing STA caller");
        Check(Apartment() == caller_apartment, "audio does not change the caller STA");
      }
      Check(Apartment() == caller_apartment, "audio cleanup does not uninitialize the caller STA");
      CoUninitialize();
    });
    run([] {
      Check(SUCCEEDED(CoInitializeEx(nullptr, COINIT_MULTITHREADED)), "existing MTA setup");
      { WindowsAudioDriver driver(nullptr, nullptr); Check(driver.Initialize(), "production driver accepts an existing MTA caller"); }
      Check(Apartment() == APTTYPE_MTA, "audio cleanup preserves caller-owned MTA");
      CoUninitialize();
    });
    std::unique_ptr<WindowsAudioDriver> transferred;
    run([&] {
      transferred = std::make_unique<WindowsAudioDriver>(nullptr, nullptr);
      Check(transferred->Initialize(), "create audio on a short-lived guest thread");
    });
    run([&] {
      Check(Apartment() == APTTYPE_MTA, "MTA outlives the creating guest thread");
      transferred.reset(); Check(true, "destroy audio safely on a different guest thread");
    });
    {
      WindowsAudioCom first, second;
      Check(SUCCEEDED(first.Initialize()) && first.Initialize() == S_FALSE,
          "repeated scope initialization does not acquire an extra cookie");
      Check(SUCCEEDED(second.Initialize()), "independent clients acquire independent MTA lifetimes");
    }
    fail_engine = true;
    { WindowsAudioDriver driver(nullptr, nullptr); Check(!driver.Initialize(), "engine creation failure returns safely without touching null voices"); }
    fail_engine = false;
    { WindowsAudioDriver driver(nullptr, nullptr); Check(driver.Initialize(), "audio can initialize again after an engine failure"); }
    std::printf("%d audio checks passed\n", checks);
    return 0;
  } catch (const std::exception& error) {
    std::printf("FAIL %s\n", error.what()); return 1;
  }
}
