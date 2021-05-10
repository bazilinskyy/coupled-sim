%% Visualize filtered data
% Author: Johnson Mok


function VisualizeDataFiltered(data)
[T,M] = countNumberTrialsPerMapping(data);
disp(T);
end

%% Helperfunction 
function [T,M] = countNumberTrialsPerMapping(data)
fld_ED = fieldnames(data);
nrVar = 5;
M = zeros(length(fld_ED),nrVar);
for ed = 1:length(fld_ED)
    number = 0;
    EDstr = split(fld_ED{ed},'_');
    Ednr = str2double(EDstr{3});
    % mapping
    if (sum(ismember(0:3,Ednr)))
        map = 0;
    elseif (sum(ismember(4:7,Ednr)))
        map = 1;
    elseif (sum(ismember(8:11,Ednr)))
        map = 2;
    end
    % condition
    if (sum(ismember([0, 4, 8],Ednr)))
        dist = 0;
        yield = 1;
    elseif (sum(ismember([1, 5, 9],Ednr)))
        dist = 0;
        yield = 0;
    elseif (sum(ismember([2, 6, 10],Ednr)))
        dist = 1;
        yield = 1;        
    elseif (sum(ismember([3, 7, 11],Ednr)))
        dist = 1;
        yield = 0;        
    end
    % frequency
    fld_part = fieldnames(data.(fld_ED{ed}).HostFixedTimeLog);
    for p = 1:length(fld_part)
        fld_trial = fieldnames(data.(fld_ED{ed}).HostFixedTimeLog.(fld_part{p}));
        number = number + length(fld_trial);
    end
    M(ed,:) = [Ednr, map, dist, yield, number];
end
T = array2table(M);
T.Properties.VariableNames(1:nrVar) = {'ED nr', 'mapping', 'distraction', 'yielding', 'Frequency'};

end
