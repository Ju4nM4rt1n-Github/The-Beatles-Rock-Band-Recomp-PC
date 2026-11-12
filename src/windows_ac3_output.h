#pragma once
#include <cstddef>
#include <string>

namespace rex::audio {
struct Ac3RearUpmixResult {
  bool applied;
  float front_rms;
  float native_rear_rms;
};

Ac3RearUpmixResult ApplyAc3RearUpmix(float* interleaved, std::size_t frames);
}

namespace rex::thread {
class Semaphore;
}
class WindowsAc3Output final {
 public:
  explicit WindowsAc3Output(rex::thread::Semaphore* semaphore,
                            bool use_event_callback = true);
  WindowsAc3Output(const WindowsAc3Output&) = delete;
  WindowsAc3Output& operator=(const WindowsAc3Output&) = delete;
  ~WindowsAc3Output();
  bool Initialize(std::string& error);
  bool Submit(const float* interleaved_256_samples);
 private:
  void Run() noexcept;
  void ProcessInput();
  bool InitializeDevice(std::string& error);
  bool RenderBuffer(bool initial, std::string& error);
  void SetStartupResult(bool success, std::string error);
  void Fail(std::string error) noexcept;
  struct Impl;
  Impl* impl_ = nullptr;
};
