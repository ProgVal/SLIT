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
        const short OTHERS = 3;
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
            buttons[OTHERS] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di3, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);

            // Initialize the button interruptions
            buttons[PLASTIC].OnInterrupt += new NativeEventHandler(onPressButton);
            buttons[METAL].OnInterrupt += new NativeEventHandler(onPressButton);
            buttons[GLASS].OnInterrupt += new NativeEventHandler(onPressButton);
            buttons[OTHERS].OnInterrupt += new NativeEventHandler(onPressButton);


            // Initialize the sensors
            sensors[PLASTIC] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di4, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);
            sensors[METAL] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di5, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);
            sensors[GLASS] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di6, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);
            sensors[OTHERS] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di7, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);

            // Initialize the sensors interruptions:
            sensors[PLASTIC].OnInterrupt += new NativeEventHandler(onSensorHighEdge);
            sensors[METAL].OnInterrupt += new NativeEventHandler(onSensorHighEdge);
            sensors[GLASS].OnInterrupt += new NativeEventHandler(onSensorHighEdge);
            sensors[OTHERS].OnInterrupt += new NativeEventHandler(onSensorHighEdge);

            // Initiliaze the status LEDs (used for debugging purpose)
            leds[PUBLIC_ERROR] = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.An0, false);
            leds[PUBLIC_WAITING] = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.An1, false);
            leds[PUBLIC_OK] = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.An2, false);
        }
        public static void onPressButton(uint port, uint state, DateTime time)
        {
            // This method is used by all buttons interruptions
            short pressedButton;
            pressedButton = (short)(port - FIRST_BUTTON_NUMBER);
            notify(PUBLIC_WAITING);
        }
        public static void onSensorHighEdge(uint port, uint state, DateTime time)
        {
            // This method is used by all sensors interruptions
            short activedSensor;
            activedSensor = (short)(port - FIRST_SENSOR_NUMBER);
            notify(PUBLIC_WAITING);
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
