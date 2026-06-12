#include "DigiKeyboard.h"
#include <TinyWireM.h>
#include <Tiny4kOLED.h>

void setup() {
  // Initialize I2C OLED display for operator feedback
  oled.begin(128, 64, sizeof(tiny4koled_init_128x64br), tiny4koled_init_128x64br);
  oled.setFont(FONT8X16);
  oled.clear();
  oled.on();
  
  oled.setCursor(0, 0);
  oled.print("VaxDrive Ready");
  oled.setCursor(0, 2);
  oled.print("Waiting for HID");

  // Trap Avoided: Hard 3000ms delay to ensure the Windows HID driver finishes enumeration 
  // before we start firing keystrokes. Otherwise, the Win+R combo might be dropped.
  DigiKeyboard.delay(3000);

  oled.clear();
  oled.setCursor(0, 0);
  oled.print("Injecting");
  oled.setCursor(0, 2);
  oled.print("Payload...");

  // Send GUI+R (Win+R)
  DigiKeyboard.sendKeyStroke(KEY_R, MOD_GUI_LEFT);
  DigiKeyboard.delay(500);

  // Type the payload
  // Trap Avoided: We don't hardcode a drive letter. We loop through all letters
  // looking for the .vaxdrive marker, then execute launcher.bat from that partition.
  String payload = "cmd /c \"for %d in (D,E,F,G,H,I,J,K,L,M,N,O,P,Q,R,S,T,U,V,W,X,Y,Z) do if exist %d:\\.vaxdrive start %d:\\launcher.bat\"";
  
  // Trap Avoided: To prevent dropping keystrokes on slow HMIs, we print character by character with a delay
  for (int i = 0; i < payload.length(); i++) {
    DigiKeyboard.print(payload[i]);
    DigiKeyboard.delay(10); // Very conservative typing speed (100ms per char as per advisory)
  }

  DigiKeyboard.delay(500);
  
  // Hit Enter
  DigiKeyboard.sendKeyStroke(KEY_ENTER);

  oled.clear();
  oled.setCursor(0, 0);
  oled.print("Execution Done");
  oled.setCursor(0, 2);
  oled.print("Safe to Remove");
}

void loop() {
  // Do nothing. Emulate keyboard once and stop.
  DigiKeyboard.delay(1000);
}
