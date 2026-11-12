#pragma once
#include <cstdint>
#include <vector>

namespace rex::audio {
inline constexpr unsigned kIec61937Ac3BurstBytes = 6144;

inline std::vector<std::uint8_t> MakeIec61937Ac3Burst(
    const std::vector<std::uint8_t>& ac3) {
  std::vector<std::uint8_t> burst(kIec61937Ac3BurstBytes, 0);
  if (ac3.size() < 6 || ac3.size() > kIec61937Ac3BurstBytes - 8 ||
      ac3.size() % 2 != 0) return {};
  const std::uint16_t data_type =
      static_cast<std::uint16_t>(0x0001 | ((ac3[5] & 0x07) << 8));
  const std::uint16_t header[] = {
      0xF872, 0x4E1F, data_type, static_cast<std::uint16_t>(ac3.size() * 8)};
  for (unsigned i = 0; i < 4; ++i) {
    burst[i * 2] = static_cast<std::uint8_t>(header[i]);
    burst[i * 2 + 1] = static_cast<std::uint8_t>(header[i] >> 8);
  }
  for (size_t i = 0; i < ac3.size(); i += 2) {
    burst[8 + i] = ac3[i + 1];
    burst[8 + i + 1] = ac3[i];
  }
  return burst;
}
}
