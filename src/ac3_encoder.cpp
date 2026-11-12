#include "ac3_encoder.h"
extern "C" {
#include <libavcodec/avcodec.h>
#include <libavutil/channel_layout.h>
#include <libavutil/error.h>
#include <libavutil/frame.h>
}

namespace {
std::string AvError(int error) {
  char text[AV_ERROR_MAX_STRING_SIZE]{};
  av_strerror(error, text, sizeof(text));
  return text;
}
}

Ac3Encoder::~Ac3Encoder() {
  av_packet_free(&packet_);
  av_frame_free(&frame_);
  avcodec_free_context(&context_);
}

bool Ac3Encoder::Initialize(std::string& error) {
  const AVCodec* codec = avcodec_find_encoder(AV_CODEC_ID_AC3);
  if (!codec) { error = "FFmpeg AC-3 encoder is unavailable"; return false; }
  context_ = avcodec_alloc_context3(codec);
  if (!context_) { error = "Could not allocate the AC-3 encoder context"; return false; }
  context_->sample_rate = 48000;
  context_->sample_fmt = AV_SAMPLE_FMT_FLTP;
  context_->bit_rate = 640000;
  context_->time_base = AVRational{1, 48000};
  int result = av_channel_layout_from_mask(&context_->ch_layout, AV_CH_LAYOUT_5POINT1_BACK);
  if (result < 0) { error = "Could not configure the 5.1 AC-3 channel layout: " + AvError(result); return false; }
  result = avcodec_open2(context_, codec, nullptr);
  if (result < 0) { error = "Could not open the 5.1 AC-3 encoder: " + AvError(result); return false; }
  if (context_->frame_size != 1536) {
    error = "The AC-3 encoder does not use 1536-sample frames";
    return false;
  }
  frame_ = av_frame_alloc();
  packet_ = av_packet_alloc();
  if (!frame_ || !packet_) { error = "Could not allocate AC-3 encoder buffers"; return false; }
  frame_->format = context_->sample_fmt;
  frame_->sample_rate = context_->sample_rate;
  frame_->nb_samples = context_->frame_size;
  result = av_channel_layout_copy(&frame_->ch_layout, &context_->ch_layout);
  if (result < 0) { error = "Could not copy the AC-3 channel layout: " + AvError(result); return false; }
  result = av_frame_get_buffer(frame_, 0);
  if (result < 0) { error = "Could not allocate the AC-3 frame: " + AvError(result); return false; }
  return true;
}

bool Ac3Encoder::Encode(const float* interleaved, std::vector<std::uint8_t>& output,
                        std::string& error) {
  if (!context_ || !frame_ || !packet_ || !interleaved) {
    error = "AC-3 encoder is not initialized";
    return false;
  }
  int result = av_frame_make_writable(frame_);
  if (result < 0) { error = "AC-3 frame is not writable: " + AvError(result); return false; }
  for (int sample = 0; sample < frame_->nb_samples; ++sample) {
    for (int channel = 0; channel < 6; ++channel) {
      reinterpret_cast<float*>(frame_->extended_data[channel])[sample] =
          interleaved[sample * 6 + channel];
    }
  }
  frame_->pts = next_pts_;
  next_pts_ += frame_->nb_samples;
  result = avcodec_send_frame(context_, frame_);
  if (result < 0) { error = "AC-3 frame submission failed: " + AvError(result); return false; }
  output.clear();
  while ((result = avcodec_receive_packet(context_, packet_)) >= 0) {
    output.assign(packet_->data, packet_->data + packet_->size);
    av_packet_unref(packet_);
  }
  if (result != AVERROR(EAGAIN) && result != AVERROR_EOF) {
    error = "AC-3 packet encoding failed: " + AvError(result);
    return false;
  }
  return true;
}

int Ac3Encoder::FrameSize() const noexcept {
  return context_ ? context_->frame_size : 0;
}
