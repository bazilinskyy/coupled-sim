% Test script to read the logfiles 
clear
clc
close all

%% Get data
% filename = "LOG_N.csv";
filename = getlatestfile("Logdata");
filename = "Logdata/" + filename;
[Data, pa, pe, diAV] = loadData(filename);

%% Time
% T = getTime(distance, dt, 1); % Set last argument to 1 to display table.

%% Graphs
figure;
grid on;
title("Distances");
yyaxis left
plot(pa.distance);
ylim([-10 100]);
ylabel("eye gaze distance passenger");
yyaxis right
plot(pa.pos.z);
ylim([-10 100]);
ylabel("AV pos_z");

figure;
grid on;
title("Distances");
yyaxis left
plot(pe.distance);
ylim([-10 100]);
ylabel("eye gaze distance pedestrian");
yyaxis right
plot(pa.pos.z);
ylim([-10 100]);
ylabel("AV pos_z");

figure;
grid on;
title("Distances");
yyaxis left
plot(pe.distance2);
ylim([-10 100]);
ylabel("eye gaze distance pedestrian 2");
yyaxis right
plot(pa.pos.z);
ylim([-10 100]);
ylabel("AV pos_z");

figure;
grid on;
title("Distances");
yyaxis left
plot(pe.distance3);
ylim([-10 100]);
ylabel("eye gaze distance pedestrian 3");
yyaxis right
plot(pa.pos.z);
ylim([-10 100]);
ylabel("AV pos_z");

figure;
grid on;
title("Distances");
yyaxis left
plot(diAV.distance);
ylim([-10 100]);
ylabel("eye gaze distance diAV");
yyaxis right
plot(pa.pos.z);
ylim([-10 100]);
ylabel("AV pos_z");

figure;
grid on;
title("Distances");
yyaxis left
plot(diAV.world.gaze_dir);
ylim([-10 100]);
ylabel("gaze_dir_diAV");
yyaxis right
plot(pa.pos.z);
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

