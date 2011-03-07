/*
Copyright (c) 2011, Valentin Lorentz
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice,
this list of conditions, and the following disclaimer.
* Redistributions in binary form must reproduce the above copyright notice,
this list of conditions, and the following disclaimer in the
documentation and/or other materials provided with the distribution.
* Neither the name of the author of this software nor the name of
contributors to this software may be used to endorse or promote products
derived from this software without specific prior written consent.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.

*/

using System;
using Microsoft.SPOT;
using System.Threading;
using Microsoft.SPOT.Hardware;
using GHIElectronics.NETMF.FEZ;

namespace MFConsoleApplication1
{
    public class Program
    {
        // Constants used for buttons and sensors arrays
        const short PLASTIC = 0;
        const short METAL = 1;
        const short GLASS = 2;
        const short MISC = 3;
        const short NUMBER_OF_SENSORS = 4;

        // Constants use for leds array
        const short PUBLIC_NO_STATE = -1;
        const short PUBLIC_ERROR = 0;
        const short PUBLIC_WAITING = 1;
        const short PUBLIC_OK = 2;
        const short NUMBER_OF_PUBLIC_STATES = 3;

        // Constants depending on the hardware
        const short FIRST_BUTTON_NUMBER = 0;
        const short FIRST_SENSOR_NUMBER = 4;
        const short FIRST_LED_NUMBER = 14;

        // Arrays
        static InterruptPort[] buttons = new InterruptPort[NUMBER_OF_SENSORS];
        static InterruptPort[] sensors = new InterruptPort[NUMBER_OF_SENSORS];
        static OutputPort[] leds = new OutputPort[NUMBER_OF_PUBLIC_STATES];

        // Internal state stuff
        const short INTERNAL_SLEEPING = 0;
        const short INTERNAL_WAITING_FOR_SENSOR = 1;
        const short INTERNAL_WAITING_FOR_BUTTON = 2;
        const short INTERNAL_WORKING = 3; // Used to be more thread-safe
        const short INTERNAL_NOTIFYING = 4; // Intermediate state between WORKING and SLEEPING
        static short currentState = INTERNAL_SLEEPING;
        static short expectedId; // Actually, it is the ID of the latest pressed button or activated sensor

        public static void Main()
        {
            initializePorts();

            // Signal startup to the operator (12 has been arbitrary choosen)
            for (int i = 0; i < 12; i++)
            {
                for (int j = 0; j < NUMBER_OF_PUBLIC_STATES; j++)
                {
                    leds[j].Write(!leds[j].Read());
                }
                Thread.Sleep(100);
            }

            Thread.Sleep(Timeout.Infinite);
        }
        public static void initializePorts()
        {
            // Method called at startup

            // Change the anti-bounce filter delay (defaults to ~200ms)
            Microsoft.SPOT.Hardware.Cpu.GlitchFilterTime = new TimeSpan(0, 0, 0, 0, 400);

            // Initialize the buttons
            buttons[PLASTIC] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di0, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);
            buttons[METAL] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di1, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);
            buttons[GLASS] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di2, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);
            buttons[MISC] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di3, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);

            // Initialize the button interruptions
            buttons[PLASTIC].OnInterrupt += new NativeEventHandler(onButtonPlastic);
            buttons[METAL].OnInterrupt += new NativeEventHandler(onButtonMetal);
            buttons[GLASS].OnInterrupt += new NativeEventHandler(onButtonGlass);
            buttons[MISC].OnInterrupt += new NativeEventHandler(onButtonMisc);


            // Initialize the sensors
            sensors[PLASTIC] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di4, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);
            sensors[METAL] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di5, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);
            sensors[GLASS] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di6, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);
            sensors[MISC] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di7, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);

            // Initialize the sensors interruptions:
            sensors[PLASTIC].OnInterrupt += new NativeEventHandler(onSensorPlastic);
            sensors[METAL].OnInterrupt += new NativeEventHandler(onSensorMetal);
            sensors[GLASS].OnInterrupt += new NativeEventHandler(onSensorGlass);
            sensors[MISC].OnInterrupt += new NativeEventHandler(onSensorMisc);

            // Initiliaze the status LEDs (used for debugging purpose)
            leds[PUBLIC_ERROR] = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.An0, false);
            leds[PUBLIC_WAITING] = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.An1, false);
            leds[PUBLIC_OK] = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.An2, false);
        }
        public static void onButtonPlastic(uint port, uint state, DateTime time) { onButton(PLASTIC); }
        public static void onButtonMetal(uint port, uint state, DateTime time) { onButton(METAL); }
        public static void onButtonGlass(uint port, uint state, DateTime time) { onButton(GLASS); }
        public static void onButtonMisc(uint port, uint state, DateTime time) { onButton(MISC); }
        public static void onButton(short pressedButton)
        {
            if (currentState == INTERNAL_WAITING_FOR_BUTTON)
            {
                currentState = INTERNAL_NOTIFYING;
                if (pressedButton == expectedId)
                    notify(PUBLIC_OK);
                else
                    notify(PUBLIC_ERROR);
            }
            else
            {
                currentState = INTERNAL_WAITING_FOR_SENSOR;
                expectedId = pressedButton;
                notify(PUBLIC_WAITING);
            }
        }
        public static void onSensorPlastic(uint port, uint state, DateTime time) { onSensor(PLASTIC); }
        public static void onSensorMetal(uint port, uint state, DateTime time) { onSensor(METAL); }
        public static void onSensorGlass(uint port, uint state, DateTime time) { onSensor(GLASS); }
        public static void onSensorMisc(uint port, uint state, DateTime time) { onSensor(MISC); }
        public static void onSensor(short activedSensor)
        {
            if (currentState == INTERNAL_WAITING_FOR_SENSOR)
            {
                currentState = INTERNAL_NOTIFYING;
                if (activedSensor == expectedId)
                    notify(PUBLIC_OK);
                else
                    notify(PUBLIC_ERROR);
            }
            else
            {
                currentState = INTERNAL_WAITING_FOR_BUTTON;
                expectedId = activedSensor;
                notify(PUBLIC_WAITING);
            }
        }
        public static void notify(short state)
        {
            // Used mainly for debugging purpose
            for (int i = 0; i < NUMBER_OF_PUBLIC_STATES; i++)
                leds[i].Write(false);
            if (0 <= state && state < NUMBER_OF_PUBLIC_STATES)
                leds[state].Write(true);
        }
    }
}
