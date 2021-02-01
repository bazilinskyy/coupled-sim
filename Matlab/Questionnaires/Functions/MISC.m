%% Misery Scale (MISC
% Author: Johnson Mok
% Last Updated: 01-02-2021

% This script analyses the acceptance questionnaire from the MISC scale
% from Nos & Mackinnon (2005). The MISC scale is used to determine the
% level of simulator motion sickness.

% Input: 
% 1) List of MISC scores as a cell array.

% Output:
% misc.val = number of selection of all experiments.
% misc.name = name of the selection options.
% misc.num = selected options converted to numbers.
% misc.mean = mean of the answers
% misc.std = standard devation of the answers
% misc.(map0/map1/map2) = same as above per mapping

function misc = MISC(dataMap, data)
%% All experiments together - number of answers
[misc.val, misc.name] = countGroupData(data);   % Number of answers selected
misc.num = convertToNum(data);                  % Misc answers converted to numbers
misc.mean = mean(misc.num);                     % Mean of all misc scores
misc.std = std(misc.num);                       % Std of all misc scores


%% MISC score per mapping - number of answers
[misc.map0.all, misc.map1.all, misc.map2.all] = matchWithMappingMISC(dataMap, data);
[misc.map0.val, ~] = countGroupData(misc.map0.all);
[misc.map1.val, ~] = countGroupData(misc.map1.all);
[misc.map2.val, ~] = countGroupData(misc.map2.all);

% Misc answers converted to numbers
misc.map0.num = convertToNum(misc.map0.all);
misc.map1.num = convertToNum(misc.map1.all);
misc.map2.num = convertToNum(misc.map2.all);

% Misc means per mapping
misc.map0.mean = mean(misc.map0.num);
misc.map1.mean = mean(misc.map1.num);
misc.map2.mean = mean(misc.map2.num);

% Misc std per mapping
misc.map0.std = std(misc.map0.num);
misc.map1.std = std(misc.map1.num);
misc.map2.std = std(misc.map2.num);

end

%% Helper functions
function [val,name]=countGroupData(data)
[GC,GR] = groupcounts(data); 
name = {'0 - No problems'; '1 - Uneasiness (no typical symptoms)';...
    '2 - Vague dizziness, warmth, headache, stomach awareness, sweating..';...
    '3 - Slight dizziness, warmth, headache, stomach awareness, sweating..';...
    '4 - Fairly dizziness, warmth, headache, stomach awareness, sweating..';...
    '5 - Severe dizziness, warmth, headache, stomach awareness, sweating..';...
    '6 - Slight naussea';...
    '7 - Fairly naussea';...
    '8 - Severe naussea';...
    '9 - (near) retching naussea';...
    '10 - Vomiting'};
val = zeros(1,length(name));
for i=1:length(name)
    index = find(strcmp(GR,name(i)));
    if(~isempty(index))
        val(i) = GC(index);
    end
end
end
function [Data_baseline, Data_map1, Data_map2] = matchWithMappingMISC(dataMap, data)
Idx0 = contains(dataMap,'Baseline');
Idx1 = contains(dataMap,'Mapping 1 (stop when looking)');
Idx2 = contains(dataMap,'Mapping 2 (stop when NOT looking');
Data_baseline = data(Idx0,:);
Data_map1     = data(Idx1,:);
Data_map2     = data(Idx2,:);
end
function out = convertToNum(data)
out = zeros(size(data));
for i = 1:length(data)
    num = num2str(i);
    idx = contains(data,num);
    out(idx) = i;
end
end



