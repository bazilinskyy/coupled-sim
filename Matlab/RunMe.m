%% Run Me
% This is the main script to perform analysis on the data.
% Author: Johnson Mok
% Last Updated: 04-02-2021
clc;
clear;
close all;

%% Add path to functions
addpath(genpath('Functions'));
addpath('Mats');

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
if(~exist('WrongData.mat'))
    load('PreDataV2.mat');
    [PreDataV3, WrongData] = FilterWrongExecutions(PreDataV2);
	save('PreDataV3.mat','PreDataV3');
    save('WrongData.mat','WrongData');
end
if(exist('PreDataV3.mat'))
    load('PreData.mat');
    load('PreDataV3.mat');   
    load('WrongData.mat');
end

%% TESTING:


%% What do we want to do with the data?
%
times = CalcTime(PreDataV3, PreData);
phases = calcPhases(PreDataV3);

% Create grouped data
timesgroup = createGroupData(times, 'time');
gapgroup = createGroupData(PreDataV3, 'gap');
rbvgroup = createGroupData(PreDataV3, 'rb_v');
pasposgroup = createGroupData(PreDataV3, 'pa_pos');
pa_distancegroup = createGroupData(PreDataV3, 'pa_distance');
pe_distancegroup = createGroupData(PreDataV3, 'pe_distance');
phasesgroup = createGroupData(phases, 'phases');

%% TESTING: Check for empty phase arrays
clc
fld_ED = fieldnames(phases);
for m=1:length(fld_ED)
    fld_par = fieldnames(phases.(fld_ED{m}).HostFixedTimeLog);
    for i=1:length(fld_par)
        fld2_trial = fieldnames(phases.(fld_ED{m}).HostFixedTimeLog.(fld_par{i}));
    %     disp(fld_par(i));
        for j=1:length(fld2_trial)
            fld3_group = fieldnames(phases.(fld_ED{m}).HostFixedTimeLog.(fld_par{i}).(fld2_trial{j}));
    %         disp(fld2_trial(j));
            for k=3%1:length(fld3_group)
                fld4_phase = fieldnames(phases.(fld_ED{m}).HostFixedTimeLog.(fld_par{i}).(fld2_trial{j}).(fld3_group{k}));
    %             disp(fld3_group(k));
                for l=1:length(fld4_phase)
    %                 disp(fld4_phase(l));
                    if(isempty(phases.(fld_ED{m}).HostFixedTimeLog.(fld_par{i}).(fld2_trial{j}).(fld3_group{k}).(fld4_phase{l})))
                        disp([fld_ED(m),fld_par(i),fld2_trial(j),fld3_group(k),fld4_phase(l)])
                    end
                end
            end
        end
    end
end
disp('done');

%% TESTING: the ones that are empty


%% Analyze data
% gazeTime = analyzeGazeTime(timesgroup, pa_distancegroup, pe_distancegroup);
gapAcpt = analyzeGapAcceptance(gapgroup, rbvgroup, pasposgroup, phasesgroup);
% posGroup = analyzePhasesGroup(phasesgroup);

%% Visualize data (needs reorganization, for now the calculations and visualization is done in the same script/function) 
% gazeTimePlotter(gazeTime); 
clc; close all;
visualizeGapAcceptance(gapAcpt);
% visulizePhasesGroup(posGroup);
