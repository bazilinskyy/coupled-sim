%% FilterLATY
% This script checks whether the participants followed the instructions
% correctly in the LATY mapping.
% Hierarchy: FilterWrongExecutions -> filterLATY
% Author: Johnson Mok

function correct = filterLATY(data, ED)
% Define (non)yielding experiment definition numbers
EDstr = split(ED,'_');
EDnr = str2double(EDstr{3});
if EDnr<8
    correct = true;
    return
end

strY = 0:2:10;
strNY = 1:2:11;

instrY = find(strY==EDnr);   % Check which condition is applied
instrNY = find(strNY==EDnr); % Check which condition is applied

if (EDnr == 8 || EDnr == 10)
    % Check for passenger gaze data in yielding condition(look away)
    gazeInRange = ismember(round(data.pa.distance,1), 14.4:0.1:25);
    gazeYield = length(find(gazeInRange==1));
    if gazeYield<60
        correct = true;
    else
        correct = false;
    end
elseif (EDnr ==9 || EDnr == 11)
    % Check for passenger gaze data in non-yielding condition(look)
    gazeInRange = ismember(round(data.pa.distance,1), 0.1:0.1:25);
    gazeYield = length(find(gazeInRange==1));
    if gazeYield>0
        correct = true;
    else
        correct = false;
    end
end
end

