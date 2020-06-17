TotalDistance = 150; %[m]
Speed = 50; %[km/h]
Conversion = 3.6; %Convert km/h to m/s

Deceleration = 3.5; %[m/s^2]

TTA = 8.64; %[s] Time of braking

Before_travel = TTA*(Speed/Conversion) %[m] Distance traveled before braking

Time_to_standstill = (Speed/Conversion)/Deceleration %[s] Time to stop
After_travel = Time_to_standstill*(Speed/Conversion)*0.5 %[m]

Total_dist = Before_travel + After_travel

