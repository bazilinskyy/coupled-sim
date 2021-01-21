%% CreateGroupData
% This script takes variables from individuals and puts those values in a long
% array, effectively creating an array of that variable.
% INPUT: result from CalcTime.m
% Author: Johnson Mok
% Last Updated: 18-01-2020

function output = createGroupData(AllData, var)
disp('Start grouping time data.');
%% create struct to input the data
fields_ED = fieldnames(AllData);
for j = 1:length(fields_ED)
    fields_time = fieldnames(AllData.(fields_ED{j}));
    for k = 1:length(fields_time)
        fields_participants = fieldnames(AllData.(fields_ED{j}).(fields_time{k}));
        if(strcmp(var,'time'))
            output.(fields_ED{j}).(fields_time{k}).eyeContact = []; %
        elseif(strcmp(var,'gap'))
            output.(fields_ED{j}).(fields_time{k}).gapAcceptance = {}; %
        end
        for idx = 1:length(fields_participants)
            fields_trials = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}));
            for i = 1:length(fields_trials)
                % Part for time
                if(strcmp(var,'time'))
                    output.(fields_ED{j}).(fields_time{k}).eyeContact(end+1) = AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).eyeContact;
                    fields_T = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}));
                    idx_TPa = find(strcmp(fields_T,'pa'));
                    idx_TPe = find(strcmp(fields_T,'pe'));
                    for l = [idx_TPa, idx_TPe]
                        fields_phase = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).(fields_T{l}));
                        if(~isfield(output.(fields_ED{j}).(fields_time{k}),(fields_T{l})))
                            output.(fields_ED{j}).(fields_time{k}).(fields_T{l}) = [];
                        end
                        for m = 1:length(fields_phase)
                            if(~isfield(output.(fields_ED{j}).(fields_time{k}).(fields_T{l}),(fields_phase{m})))
                                output.(fields_ED{j}).(fields_time{k}).(fields_T{l}).(fields_phase{m}) = [];
                            end
                            fields_var = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).(fields_T{l}).(fields_phase{m}));
                            for n = 1:length(fields_var)
                                idx_dis = find(strcmp(fields_var,'distance'));
                                if(n~=idx_dis)
                                    if(~isfield(output.(fields_ED{j}).(fields_time{k}).(fields_T{l}).(fields_phase{m}),(fields_var{n})))
                                        output.(fields_ED{j}).(fields_time{k}).(fields_T{l}).(fields_phase{m}).(fields_var{n}) = [];
                                    end
                                    output.(fields_ED{j}).(fields_time{k}).(fields_T{l}).(fields_phase{m}).(fields_var{n}) = [output.(fields_ED{j}).(fields_time{k}).(fields_T{l}).(fields_phase{m}).(fields_var{n}), AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).(fields_T{l}).(fields_phase{m}).(fields_var{n})];
                                end
                            end
                        end
                    end
                end
                % Part for gapAcceptance
                if(strcmp(var,'gap'))
                    output.(fields_ED{j}).(fields_time{k}).gapAcceptance(end+1,:) = {AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pe.gapAcceptance};
                end
            end
        end
    end
end


disp('Finish grouping time data.');
end
