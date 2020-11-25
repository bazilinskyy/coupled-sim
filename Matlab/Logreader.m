% Test script to read the logfiles 
clear
clc
close all

%% Get data
% filename = "nonfixed1.csv";
filename = getlatestfile("Logdata");
filename = "Logdata/" + filename;
[Data, pa, pe, diAV, M] = loadData(filename, false); % Set last argument to true if there is an extra AV

%% Time
% T = getTime(pa.distance, Data.dt, 0); % Set last argument to 1 to display table.

%% Graphs
dataPlotter(Data, pa, pe, diAV);

%%
figure;
plot(pe.gapAcceptance);
ylim([-2 2]);
