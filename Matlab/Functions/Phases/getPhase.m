%% Get Phases
% This script calculates all the gazing times.
% Hierarchy: calcPhases -> getPhases -> calcPhaseIdx

% 1)start sound till start trigger range
% 2)start trigger range till end trigger range
% 3)end trigger range till standstill location AV
% 4)standstill location AV till past the zebra crossing

% Author: Johnson Mok
% Last Updated: 05-02-2021

function t = getPhase(data)
pos = data.pa.pos.z;


t.test = data.expdefNr;
end