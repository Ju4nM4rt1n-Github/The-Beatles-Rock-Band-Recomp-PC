#pragma once
#include <rex/cvar.h>
#include <rex/input/input.h>
REXCVAR_DECLARE(std::string, input_instrument);
inline bool DrumsSelected() { return REXCVAR_GET(input_instrument) == "drums"; }
inline bool DrumsSelected(const std::string& instrument) { return instrument == "drums"; }
inline bool VocalsSelected() { return REXCVAR_GET(input_instrument) == "vocals"; }
inline bool VocalsSelected(const std::string& instrument) { return instrument == "vocals"; }
inline void ConfigureInstrumentCaps(rex::input::X_INPUT_CAPABILITIES& caps, bool drums, bool vocals = false) {
  caps = {};
  caps.type = rex::input::XINPUT_DEVTYPE_GAMEPAD;
  if (vocals) {
    // A plain pad for the singer: the microphone itself is reported separately.
    caps.sub_type = rex::input::XINPUT_DEVSUBTYPE_GAMEPAD;
    caps.gamepad.buttons = 0xf3ff;
    return;
  }
  caps.sub_type = drums ? rex::input::XINPUT_DEVSUBTYPE_DRUM_KIT
                        : rex::input::XINPUT_DEVSUBTYPE_GUITAR;
  caps.gamepad.buttons = drums ? 0xf13f : 0xf33f;
  if (!drums) caps.gamepad.thumb_rx = INT16_MAX;
}
inline void ConfigureInstrumentCaps(rex::input::X_INPUT_CAPABILITIES& caps) {
  ConfigureInstrumentCaps(caps, DrumsSelected(), VocalsSelected());
}
