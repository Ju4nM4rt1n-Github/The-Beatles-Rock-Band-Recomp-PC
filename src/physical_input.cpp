#include <windows.h>
#include <array>
#include <sstream>
#include <string>
#include <rex/cvar.h>
#include <rex/input/input.h>
#include "physical_input.h"
#include "controller_bridge.h"
#include "input_instrument.h"
REXCVAR_DEFINE_STRING(controller_device,"keyboard","Input","Physical controller identity selected in the launcher; keyboard selects keyboard input");
REXCVAR_DEFINE_STRING(controller_bindings,"","Input","Physical guitar action bindings supplied by the launcher");
REXCVAR_DEFINE_STRING(controller_device_1,"","Input","Physical controller identity for XInput player 2");
REXCVAR_DEFINE_STRING(controller_bindings_1,"","Input","Physical controller bindings for XInput player 2");
REXCVAR_DEFINE_STRING(controller_instrument_1,"guitar","Input","Instrument type for XInput player 2");
REXCVAR_DEFINE_STRING(controller_device_2,"","Input","Physical controller identity for XInput player 3");
REXCVAR_DEFINE_STRING(controller_bindings_2,"","Input","Physical controller bindings for XInput player 3");
REXCVAR_DEFINE_STRING(controller_instrument_2,"guitar","Input","Instrument type for XInput player 3");
REXCVAR_DEFINE_STRING(controller_device_3,"","Input","Physical controller identity for XInput player 4");
REXCVAR_DEFINE_STRING(controller_bindings_3,"","Input","Physical controller bindings for XInput player 4");
REXCVAR_DEFINE_STRING(controller_instrument_3,"guitar","Input","Instrument type for XInput player 4");
namespace {
const std::array<const char*,15> actions={"green","red","yellow","blue","orange","strum_up","strum_down","whammy","star_power","solo","start","menu_left","menu_right","accept","cancel"};
constexpr std::array<std::uint16_t,15> buttons={0x1000,0x2000,0x8000,0x4000,0x100,1,2,0,0x20,0x200,0x10,4,8,0x1000,0x2000};
int PlayerIndex(PPCContext& ctx) {
 auto user=ctx.r3.u32,flags=ctx.r4.u32;
 if(user<=3)return int(user);
 if(user!=0&&(user&255)!=255&&!(flags&(1u<<30)))return -1;
 return 0;
}
const std::string& DeviceForPlayer(int player) {
 if(player==1)return REXCVAR_GET(controller_device_1);
 if(player==2)return REXCVAR_GET(controller_device_2);
 if(player==3)return REXCVAR_GET(controller_device_3);
 return REXCVAR_GET(controller_device);
}
std::string InstrumentForPlayer(int player) {
 if(player==1)return REXCVAR_GET(controller_instrument_1);
 if(player==2)return REXCVAR_GET(controller_instrument_2);
 if(player==3)return REXCVAR_GET(controller_instrument_3);
 return REXCVAR_GET(input_instrument);
}
const std::string& BindingsForPlayer(int player) {
 if(player==1)return REXCVAR_GET(controller_bindings_1);
 if(player==2)return REXCVAR_GET(controller_bindings_2);
 if(player==3)return REXCVAR_GET(controller_bindings_3);
 return REXCVAR_GET(controller_bindings);
}
bool Read(const std::string& device,BR_State&state){
 return BR_Read(device.c_str(),&state)!=0;
}
}
bool PhysicalInputCapabilities(PPCContext&ctx,std::uint8_t*base){
 int player=PlayerIndex(ctx);if(player<0)return false;
 const auto&device=DeviceForPlayer(player);if(device.empty()||device=="keyboard")return false;
 BR_State state{};if(!ctx.r5.u32||!Read(device,state)){ctx.r3.u64=1167;return true;}
 auto&caps=*reinterpret_cast<rex::input::X_INPUT_CAPABILITIES*>(base+ctx.r5.u32);caps={};
 ConfigureInstrumentCaps(caps,DrumsSelected(InstrumentForPlayer(player)),VocalsSelected(InstrumentForPlayer(player)));ctx.r3.u64=0;return true;
}
bool PhysicalInputState(PPCContext&ctx,std::uint8_t*base){
 int player=PlayerIndex(ctx);if(player<0)return false;
 const auto&device=DeviceForPlayer(player);if(device.empty()||device=="keyboard")return false;
 BR_State raw{};if(!ctx.r5.u32||!Read(device,raw)){ctx.r3.u64=1167;return true;}
 static const auto bindings=[](){std::array<std::array<std::string,15>,4> result{};for(int player=0;player<4;player++){std::istringstream stream(BindingsForPlayer(player));std::string pair;while(std::getline(stream,pair,';')){auto split=pair.find('=');if(split==std::string::npos)continue;for(unsigned i=0;i<actions.size();i++)if(pair.substr(0,split)==actions[i])result[player][i]=pair.substr(split+1);}}return result;}();
 auto&out=*reinterpret_cast<rex::input::X_INPUT_STATE*>(base+ctx.r5.u32);out={};
 const bool drums=DrumsSelected(InstrumentForPlayer(player)),vocals=VocalsSelected(InstrumentForPlayer(player)),noWhammy=drums||vocals;
 static std::uint32_t packet=0;out.packet_number=++packet;out.gamepad.thumb_rx=noWhammy?0:INT16_MIN;
 DWORD foreground=0;GetWindowThreadProcessId(GetForegroundWindow(),&foreground);
 if(foreground==GetCurrentProcessId()){
    std::uint16_t mask=0;for(unsigned i=0;i<actions.size();i++){if(noWhammy&&(i==7||i==9))continue;float value=BR_Value(&raw,bindings[player][i].c_str());if(i==7)out.gamepad.thumb_rx=static_cast<std::int16_t>(-32768+value*65535);else if(value>.5f)mask|=buttons[i];}out.gamepad.buttons=mask;
 }
 ctx.r3.u64=0;return true;
}
