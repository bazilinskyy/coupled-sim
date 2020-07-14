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
