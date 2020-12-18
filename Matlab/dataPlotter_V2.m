%% Data plotter
% This script plots the following data:
% -
% Author: Johnson Mok
% Last Updated: 18-12-2020

function dataPlotter_V2(Data)
disp('Start plotting data...');

%% Plots for the passenger 
% pos_xyz vs time
figure;
grid on;
hold on;
plot(Data.Time, Data.pa.pos.x, 'b');
plot(Data.Time, Data.pa.pos.y, 'r');
plot(Data.Time, Data.pa.pos.z, 'g');
ylabel('Position of the passenger in world coordinates [m]');
legend('x','y','z');
title('Position passenger vs Time');

% rot_xyz vs time
figure;
grid on;
hold on;
plot(Data.Time, Data.pa.rot.x, 'b');
plot(Data.Time, Data.pa.rot.y, 'r');
plot(Data.Time, Data.pa.rot.z, 'g');
ylabel('Rotation of the passenger in world coordinates [m]');
legend('x','y','z');
title('Rotation passenger vs Time');

% eye-gaze distance vs time
figure;
grid on;
hold on;
plot(Data.Time, Data.pa.distance);
ylabel('Distance of passenger gaze with pedestrian [m]');
title('Distance passenger gaze vs Time');

% left and right eye pupilsize vs time
figure;
grid on;
hold on;
plot(Data.Time, Data.pa.other.leftEyePupilSize, 'b');
plot(Data.Time, Data.pa.other.rightEyePupilSize, 'r');
ylabel('Eye pupil sizse');
legend('left','right');
title('Eye pupil size vs Time');

% left and right eye pupilsize vs time
figure;
grid on;
hold on;
plot(Data.Time, Data.pa.other.focusDistance, 'b');
plot(Data.Time, Data.pa.other.focusStability, 'r');
ylabel('Eye pupil sizse');
legend('focus distance','focus stability');
title('Focus vs Time');

% hmd gaze dir vs time
figure;
grid on;
hold on;
plot(Data.Time, Data.pa.HMD.gaze_dir.x, 'b');
plot(Data.Time, Data.pa.HMD.gaze_dir.y, 'r');
plot(Data.Time, Data.pa.HMD.gaze_dir.z, 'g');
ylabel('HMD gaze direction');
legend('x','y','z');
title('HMD gaze direction vs Time');

% hmd gaze origin vs time
figure;
grid on;
hold on;
plot(Data.Time, Data.pa.HMD.gaze_origin.x, 'b');
plot(Data.Time, Data.pa.HMD.gaze_origin.y, 'r');
plot(Data.Time, Data.pa.HMD.gaze_origin.z, 'g');
ylabel('HMD origin direction');
legend('x','y','z');
title('HMD gaze origin vs Time');

% world gaze dir vs time
figure;
grid on;
hold on;
plot(Data.Time, Data.pa.world.gaze_dir.x, 'b');
plot(Data.Time, Data.pa.world.gaze_dir.y, 'r');
plot(Data.Time, Data.pa.world.gaze_dir.z, 'g');
ylabel('world gaze direction');
legend('x','y','z');
title('world gaze direction vs Time');

% world gaze origin vs time
figure;
grid on;
hold on;
plot(Data.Time, Data.pa.world.gaze_origin.x, 'b');
plot(Data.Time, Data.pa.world.gaze_origin.y, 'r');
plot(Data.Time, Data.pa.world.gaze_origin.z, 'g');
ylabel('world origin direction');
legend('x','y','z');
title('world gaze origin vs Time');

% world rigid body velocity vs time
figure;
grid on;
hold on;
plot(Data.Time, Data.pa.world.rb_v.x, 'b');
plot(Data.Time, Data.pa.world.rb_v.y, 'r');
plot(Data.Time, Data.pa.world.rb_v.z, 'g');
ylabel('World passenger vehicle velocity [m/s2]');
legend('x','y','z');
title('World passenger vehicle velocity vs Time');

% local rigid body velocity vs time
figure;
grid on;
hold on;
plot(Data.Time, Data.pa.local.rb_v.x, 'b');
plot(Data.Time, Data.pa.local.rb_v.y, 'r');
plot(Data.Time, Data.pa.local.rb_v.z, 'g');
ylabel('Local passenger vehicle velocity [m/s2]');
legend('x','y','z');
title('Local passenger vehicle velocity vs Time');

%%
disp('Finished plotting data');
end