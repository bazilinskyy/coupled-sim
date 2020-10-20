% Test script to read the logfiles 
clear
clc
close all

%% Get data
% filename = "LOG_N.csv";
filename = getlatestfile("Logdata");
filename = "Logdata/" + filename;
Data = loadData(filename);
% Data = loadData("Logdata/a1.csv");

%% Variables to use
% General
dt = Data.dt;

% Passenger
distance        = Data.pa.distance;
pos_z           = Data.pa.pos.z;
world_origin    = Data.pa.world.gaze_origin;
hmd_origin      = Data.pa.HMD.gaze_origin;
world_dir       = Data.pa.world.gaze_dir;

% Pedestrian
dis_pe = Data.pe.distance;

%% Time
T = getTime(distance, dt, 1); % Set last argument to 1 to display table.

%% Graphs
figure;
grid on;
title("Distances");
yyaxis left
plot(distance);
ylim([-10 100]);
ylabel("eye gaze distance passenger");
yyaxis right
plot(pos_z);
ylim([-10 100]);
ylabel("AV pos_z");

figure;
grid on;
title("Distances");
yyaxis left
plot(dis_pe);
ylim([-10 100]);
ylabel("eye gaze distance pedestrian");
yyaxis right
plot(pos_z);
ylim([-10 100]);
ylabel("AV pos_z");

% figure;
% subplot(3,1,1);
% plot(world_origin.x);
% ylabel("world origin x");
% title("World origin");
% subplot(3,1,2);
% plot(world_origin.y);
% ylabel("world origin y");
% subplot(3,1,3);
% plot(world_origin.z);
% ylabel("world origin z");
% 
% 
% figure;
% subplot(3,1,1);
% plot(hmd_origin.x);
% ylabel("hmd origin x");
% title("HMD origin");
% subplot(3,1,2);
% plot(hmd_origin.y);
% ylabel("hmd origin y");
% subplot(3,1,3);
% plot(hmd_origin.z);
% ylabel("hmd origin z");
% 
% figure;
% subplot(3,1,1);
% plot(world_dir.x);
% ylabel("world dir x");
% title("world dir");
% subplot(3,1,2);
% plot(world_dir.y);
% ylabel("world dir y");
% subplot(3,1,3);
% plot(world_dir.z);
% ylabel("world dir z");

