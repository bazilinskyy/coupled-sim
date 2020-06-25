% Test script to read the logfiles 
clear
clc
close all

%% Get data
filename = "LOG_N.csv";
Data = loadData(filename);

% Variables to use
distance = Data.distance;
dt = Data.dt;
pos_z = Data.pos.z;

%% Time
T = getTime(distance, dt, 1);

%% Graphs
figure;
yyaxis left
plot(distance);
yyaxis right
plot(pos_z);
