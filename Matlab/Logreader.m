% Test script to read the logfiles 
clear
clc
close all

%% Get data
% filename = "LOG_N.csv";
filename = getlatestfile("Logdata");
filename = "Logdata/" + filename;
[Data, pa, pe, diAV, M] = loadData(filename, false);

%% Time
T = getTime(pa.distance, Data.dt, 0); % Set last argument to 1 to display table.

%% Graphs
dataPlotter(Data, pa, pe, diAV);


