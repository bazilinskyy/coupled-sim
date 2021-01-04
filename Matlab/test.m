clc
clear
close all

%%
load('AllData.mat'); 

%%
Data = ParentData.Data_ED_0.HostFixedTimeLog.participant_1.trial_14;

% Set start and end of: Time, pa, diAV, pe
fieldsInTrial = fieldnames(Data);
Index_Time = find(strcmp(fieldsInTrial, 'Time'));
Index_pa = find(strcmp(fieldsInTrial, 'pa'));
Index_pe = find(strcmp(fieldsInTrial, 'pe'));
arr = [Index_pa, Index_pe];
if(~isnan(Data.diAV))
    Index_diAV = find(strcmp(fieldsInTrial, 'diAV'));
    arr = [Index_pa, Index_pe, Index_diAV];
end

for index = 1:length(arr)
    disp('diu');
input = ParentData.Data_ED_0.HostFixedTimeLog.participant_1.trial_14.(fieldsInTrial{arr(index)});
lvl1 = fun(input);
if(~iscell(lvl1))
    ParentData.Data_ED_0.HostFixedTimeLog.participant_1.trial_14.(fieldsInTrial{arr(index)}).distance = lvl1;
elseif(iscell(lvl1))
    for i=1:length(lvl1)
        lvl2 = fun(ParentData.Data_ED_0.HostFixedTimeLog.participant_1.trial_14.(fieldsInTrial{arr(index)}).(lvl1{i}));
        if(~iscell(lvl2))
            ParentData.Data_ED_0.HostFixedTimeLog.participant_1.trial_14.(fieldsInTrial{arr(index)}).(lvl1{i}) = lvl2;
        elseif(iscell(lvl2))
            for j=1:length(lvl2)
                lvl3 = fun(ParentData.Data_ED_0.HostFixedTimeLog.participant_1.trial_14.(fieldsInTrial{arr(index)}).(lvl1{i}).(lvl2{j}));
                if(~iscell(lvl3))
                    ParentData.Data_ED_0.HostFixedTimeLog.participant_1.trial_14.(fieldsInTrial{arr(index)}).(lvl1{i}).(lvl2{j}) = lvl3;
                elseif(iscell(lvl3))    
                    for k=1:length(lvl3)
                        lvl4 = fun(ParentData.Data_ED_0.HostFixedTimeLog.participant_1.trial_14.(fieldsInTrial{arr(index)}).(lvl1{i}).(lvl2{j}).(lvl3{k}));
                        if(~iscell(lvl4))
                            ParentData.Data_ED_0.HostFixedTimeLog.participant_1.trial_14.(fieldsInTrial{arr(index)}).(lvl1{i}).(lvl2{j}).(lvl3{k}) = lvl4;
                        elseif(iscell(lvl4))
                            disp('not end')
                        end
                    end
                end
            end
        end
    end
end
end

%check
size(ParentData.Data_ED_0.HostFixedTimeLog.participant_1.trial_14.pa.distance)

function output = fun(input)
    if(isstruct(input))
        output = fieldnames(input);
        % Could use this code if it is not needed to maintain the data
        % structure order.
    %     for i=1:length(fields)
    %         fun(input.(fields{i}));
    %     end
    elseif(~isstruct(input))
        output = input(1:10);
    end
end


