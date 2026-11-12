#pragma once
#include <cstdint>
#include <string>
#include <vector>
struct AVCodecContext;
struct AVFrame;
struct AVPacket;
class Ac3Encoder final {
 public:
  Ac3Encoder() = default;
  Ac3Encoder(const Ac3Encoder&) = delete;
  Ac3Encoder& operator=(const Ac3Encoder&) = delete;
  ~Ac3Encoder();
  bool Initialize(std::string& error);
  bool Encode(const float* interleaved, std::vector<std::uint8_t>& packet,
              std::string& error);
  int FrameSize() const noexcept;
 private:
  AVCodecContext* context_ = nullptr;
  AVFrame* frame_ = nullptr;
  AVPacket* packet_ = nullptr;
  std::int64_t next_pts_ = 0;
};
