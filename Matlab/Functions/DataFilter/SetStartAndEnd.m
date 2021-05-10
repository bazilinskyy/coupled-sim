%% Calibrate start and end point
% This script sets the position of the 'press now' button as the start (0)
% and the point where AV is at standstill for 2.6 s as the end point for
% the yielding conditions. For the non-yielding conditions, the point where
% the AV passes the zebra crossing is the end point.
% Author: Johnson Mok

clc
clear
close all

disp('Start removing data before start and end point.');
%% Load pre-processed data
load('PreData.mat');        

%%
fields_ED = fieldnames(PreData);
for j = 1:length(fields_ED)
    fields_time = fieldnames(PreData.(fields_ED{j}));
    for k = 1:length(fields_time)
        fields_participants = fieldnames(PreData.(fields_ED{j}).(fields_time{k}));
        for idx = 1:length(fields_participants)
            fields_trials = fieldnames(PreData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}));
            for i = 1:length(fields_trials)
                %% Perform size reduction on trials
%                 PreData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i});
                trial_start = find(PreData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).sound.press>0, 1, 'first');
                trial_end   = find(PreData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).sound.release>0, 1, 'first');
                % Set start and end of: Time, pa, diAV, pe
                fieldsInTrial = fieldnames(PreData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}));
                Index_Time = find(strcmp(fieldsInTrial, 'Time'));
                Index_pa = find(strcmp(fieldsInTrial, 'pa'));
                Index_pe = find(strcmp(fieldsInTrial, 'pe'));
                arr = [Index_pa, Index_pe];
                if(isstruct(PreData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).diAV))
                    Index_diAV = find(strcmp(fieldsInTrial, 'diAV'));
                    arr = [Index_pa, Index_pe, Index_diAV];
                end
                PreData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).(fieldsInTrial{Index_Time}) = PreData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).(fieldsInTrial{Index_Time})(trial_start:trial_end); % Time
                % The rest
                for index = 1:length(arr)
                lvl1 = fun(PreData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).(fieldsInTrial{arr(index)}), trial_start, trial_end);
                if(~iscell(lvl1))
                    PreData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).(fieldsInTrial{arr(index)}).distance = lvl1;
                    disp('Size distance is: ');
                    disp(size(PreData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).(fieldsInTrial{arr(index)}).distance));%%%%%
                elseif(iscell(lvl1))
                    for L=1:length(lvl1)
                        lvl2 = fun(PreData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).(fieldsInTrial{arr(index)}).(lvl1{L}), trial_start, trial_end);
                        if(~iscell(lvl2))
                            PreData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).(fieldsInTrial{arr(index)}).(lvl1{L}) = lvl2;
                        elseif(iscell(lvl2))
                            for M=1:length(lvl2)
                                lvl3 = fun(PreData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).(fieldsInTrial{arr(index)}).(lvl1{L}).(lvl2{M}), trial_start, trial_end);
                                if(~iscell(lvl3))
                                    PreData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).(fieldsInTrial{arr(index)}).(lvl1{L}).(lvl2{M}) = lvl3;
                                elseif(iscell(lvl3))    
                                    for N=1:length(lvl3)
                                        lvl4 = fun(PreData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).(fieldsInTrial{arr(index)}).(lvl1{L}).(lvl2{M}).(lvl3{N}), trial_start, trial_end);
                                        if(~iscell(lvl4))
                                            PreData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).(fieldsInTrial{arr(index)}).(lvl1{L}).(lvl2{M}).(lvl3{N}) = lvl4;
                                        elseif(iscell(lvl4))
                                            disp('Need to code past lvl 4')
                                        end
                                    end
                                end
                            end
                        end
                    end
                end
                end
            end
        end
    end
end

PreDataV2 = PreData;
save('PreDataV2.mat', 'PreDataV2', '-v7.3');
disp('Finished removing data before start sign and after stop sign.');

function output = fun(input, trial_start, trial_end)
    if(isstruct(input))
        output = fieldnames(input);
        % Could use this code if it is not needed to maintain the data structure order.
    %     for i=1:length(fields)
    %         fun(input.(fields{i}));
    %     end
    elseif(~isstruct(input))
        output = input(trial_start:trial_end);
    end
end

