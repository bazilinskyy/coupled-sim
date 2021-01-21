%% Run Me
% This is the main script to perform analysis on the data.
% Author: Johnson Mok
% Last Updated: 03-01-2020
clc;
clear;
close all;

%% Load Data 
if(~exist('AllData.mat'))
% Notice: depending on the number and size of the logfiles, it will take multiple hours to create the datastructure. 
    run('FileOrdener.m');       % Orden all files from log data to the file structure.
    run('DataStructCreator.m'); % Convert CSV files to matlab data structure and save it.
end
if(~exist('PreData.mat'))
    load('AllData.mat');                % Load pre-converted data
    PreData = PreProcess(ParentData);   % Remove faulty data
	save('PreData.mat','PreData');
end
if(~exist('PreDataV2.mat'))
    load('PreData.mat');
    run('SetStartAndEnd.m');            % Remove data before and after sound signals
end
if(exist('PreDataV2.mat'))
    load('PreData.mat');
    load('PreDataV2.mat');          
end

%% What do we want to do with the data?
% execute calculation functions here
times = CalcTime(PreDataV2, PreData);
timesgroup = createGroupData(times, 'time');
gapgroup = createGroupData(PreDataV2, 'gap');




%% Visualize data 
% execute plot data functions here
% Function to plot gaze times
gazeTimePlotter(timesgroup);

%% test
visualizeGapAcceptance(gapgroup);


% dataPlotter_V2(PreData.Data_ED_0.HostFixedTimeLog.participant_1.trial_14,'pedestrian');
% PlotAll(PreData);

    % gap acceptance vs time
%     figure;
%     grid on;
%     hold on;
%     plot(PreDataV2.Data_ED_0.HostFixedTimeLog.participant_1.trial_8.Time, PreDataV2.Data_ED_0.HostFixedTimeLog.participant_1.trial_8.pa.world.rb_v.z, 'g');
%     plot(PreDataV2.Data_ED_0.HostFixedTimeLog.participant_1.trial_8.Time, PreDataV2.Data_ED_0.HostFixedTimeLog.participant_1.trial_8.pa.pos.z, 'r');
%     yline(23.19);
%     ylabel('Gap acceptance');
%     title('Gap acceptance vs Time');