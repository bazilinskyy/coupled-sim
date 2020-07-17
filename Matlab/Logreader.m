% Test script to read the logfiles 
clear
clc
close all

%% Get data
% filename = "LOG_N.csv";
filename = getlatestfile("Logdata");
filename = "Logdata/" + filename;
Data = loadData(filename);

% Variables to use
distance = Data.distance;
dt = Data.dt;
pos_z = Data.pos.z;
world_origin = Data.world.gaze_origin;
hmd_origin = Data.HMD.gaze_origin;
world_dir = Data.world.gaze_dir;

%% Time
T = getTime(distance, dt, 1); % Set last argument to 1 to display table.

%% Graphs
figure;
yyaxis left
plot(distance);
ylabel("eye gaze distance");
yyaxis right
plot(pos_z);
ylabel("AV pos_z");

figure;
subplot(3,1,1);
plot(world_origin.x);
ylabel("world origin x");
subplot(3,1,2);
plot(world_origin.y);
ylabel("world origin y");
subplot(3,1,3);
plot(world_origin.z);
ylabel("world origin z");

figure;
subplot(3,1,1);
plot(hmd_origin.x);
ylabel("hmd origin x");
subplot(3,1,2);
plot(hmd_origin.y);
ylabel("hmd origin y");
subplot(3,1,3);
plot(hmd_origin.z);
ylabel("hmd origin z");

figure;
subplot(3,1,1);
plot(world_dir.x);
ylabel("hmd origin x");
subplot(3,1,2);
plot(hmd_origin.y);
ylabel("hmd origin y");
subplot(3,1,3);
plot(hmd_origin.z);
ylabel("hmd origin z");
