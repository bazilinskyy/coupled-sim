%% Calc phases
% This script splits up all data into phases.
% Hierarchy: calcPhasesV2 -> getPhasesV2 

% Yielding
% 1)start sound till start trigger range
% 2)start trigger range till standstill location AV
% 3)standstill location AV till at standstill for 2.6 s

% Non-Yielding
% 1)start sound till start trigger range
% 2)start trigger range till AV past zebra crossing

% Author: Johnson Mok

function T = calcPhasesV2(AllData)
fields_ED = fieldnames(AllData);
for j = 1:length(fields_ED)
    fields_time = fieldnames(AllData.(fields_ED{j}));
    for k = 1:length(fields_time)
        fields_participants = fieldnames(AllData.(fields_ED{j}).(fields_time{k}));
        for idx = 1:length(fields_participants)
            fields_trials = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}));
            for i = 1:length(fields_trials)
                % Perform task here
                T.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}) = getPhaseV2(...
                    AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}),...
                    fields_ED{j});
            end
        end
    end
end
end