%% Get time
% This script calculates all the gazing times.
% Hierarchy: CalcTimeV2 -> getTimeV3 -> calcGazeTime
% Author: Johnson Mok

% Yielding
% 1)start sound till start trigger range
% 2)start trigger range till standstill location AV
% 3)standstill location AV till at standstill for 2.6 s

% Non-Yielding
% 1)start sound till start trigger range
% 2)start trigger range till AV past zebra crossing

function t = getTimeV3(data, phase)
% Correct distance eye-gaze
corr_dis_pa = correctEyeGazeDistance(data.pa.distance, data.pa.pos.z);
corr_dis_pe = correctEyeGazeDistance(data.pe.distance, data.pa.pos.z);

% Times
t.pa = calcPhaseGazeTimes(corr_dis_pa, phase.idx, data.dt);
t.pe = calcPhaseGazeTimes(corr_dis_pe, phase.idx, data.dt);

% Passenger and Pedstrian
if(size(phase.idx,2)>2)
    corr_dis_pa2 = correctEyeGazeDistance(data.pa.distance(phase.idx(1,2):phase.idx(2,3)), data.pa.pos.z(phase.idx(1,2):phase.idx(2,3)));
    corr_dis_pe2 = correctEyeGazeDistance(data.pe.distance(phase.idx(1,2):phase.idx(2,3)), data.pa.pos.z(phase.idx(1,2):phase.idx(2,3)));
else
    corr_dis_pa2 = correctEyeGazeDistance(data.pa.distance(phase.idx(1,2):phase.idx(2,2)), data.pa.pos.z(phase.idx(1,2):phase.idx(2,2)));
    corr_dis_pe2 = correctEyeGazeDistance(data.pe.distance(phase.idx(1,2):phase.idx(2,2)), data.pa.pos.z(phase.idx(1,2):phase.idx(2,2)));
end
pa_watch = corr_dis_pa2>0;
pe_watch = corr_dis_pe2>0;
t_eyeContact = pa_watch+pe_watch;
t.eyeContact = sum(t_eyeContact==2)*data.dt;
end

%% Helper function
function out = correctEyeGazeDistance(distance, posz)
posz    = posz-17.19;
idxdis  = find(distance>0);
diff    = distance(idxdis)-posz(idxdis);
distance(idxdis) = distance(idxdis) - diff;
out     = distance;
end
function t = calcPhaseGazeTimes(distance, idx, dt)
if(size(idx,2)>2)
t.phase1 = calcGazeTime(distance, dt, idx(1,1), idx(2,1));
t.phase2 = calcGazeTime(distance, dt, idx(1,2), idx(2,2));
t.phase3 = calcGazeTime(distance, dt, idx(1,3), idx(2,3));
t.full   = calcGazeTime(distance, dt, idx(1,1), idx(2,3));
else
    t.phase1 = calcGazeTime(distance, dt, idx(1,1), idx(2,1));
    t.phase2 = calcGazeTime(distance, dt, idx(1,2), idx(2,2));
    t.full   = calcGazeTime(distance, dt, idx(1,1), idx(2,2));
end
end
