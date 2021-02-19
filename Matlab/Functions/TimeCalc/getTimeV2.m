%% Get time
% This script calculates all the gazing times.
% Hierarchy: CalcTime -> getTime -> calcGazeTime
% Author: Johnson Mok
% Last Updated: 19-01-2020
% function output = getTime(distance, vel, dt, display)

function t = getTimeV2(data, phase)
% Correct distance eye-gaze
corr_dis_pa = correctEyeGazeDistance(data.pa.distance, data.pa.pos.z);
corr_dis_pe = correctEyeGazeDistance(data.pe.distance, data.pa.pos.z);

% Times
t.pa = calcPhaseGazeTimes(corr_dis_pa, phase.idx, data.dt);
t.pe = calcPhaseGazeTimes(corr_dis_pe, phase.idx, data.dt);

% Passenger and Pedstrian
pa_watch = corr_dis_pa>0;
pe_watch = corr_dis_pe>0;
t_eyeContact = pa_watch+pe_watch;
% t.eyeContact.arr = t_eyeContact==2;
t.eyeContact = sum(t_eyeContact==2)*data.dt;
end

%% Helper function
function out = correctEyeGazeDistance(distance, posz)
posz = posz-17.19;
idxdis = find(distance>0);
diff = distance(idxdis)-posz(idxdis);
distance(idxdis) = distance(idxdis) - diff;
out = distance;
% figure
% hold on
% plot(out)
% plot(posz)
end
function t = calcPhaseGazeTimes(distance, idx, dt)
% 1. Start sound press now – Start Detect range of trigger.
t.phase1 = calcGazeTime(distance, dt, idx(1,1), idx(2,1));
% 2. Start detect trigger range – End  detect range of trigger
t.phase2 = calcGazeTime(distance, dt, idx(1,2), idx(2,2));
% 3. End detect range of trigger – standstill
t.phase3 = calcGazeTime(distance, dt, idx(1,3), idx(2,3));
if(length(idx)>3)
    % 4. Standstill
    t.phase4 = calcGazeTime(distance, dt, idx(1,4), idx(2,4));
    % 5. AV start driving again. 
    t.phase5 = calcGazeTime(distance, dt, idx(1,5), idx(2,5));
    % 0. Full run
    t.full = calcGazeTime(distance, dt, idx(1,1), idx(2,5));
%     t.time_total = length(idx(1,1):idx(2,5))*dt;
else
    t.full = calcGazeTime(distance, dt, idx(1,1), idx(2,3));
%     t.time_total = length(idx(1,1):idx(2,3))*dt;
end
end
