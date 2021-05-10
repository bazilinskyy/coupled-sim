%% Help function 
% Hierarchy: CalcTimeV2 -> getTimeV3 -> calcGazeTime
function out = calcGazeTime(distance,dt,start,endd)
out.distance = distance(start:endd);
idx_nowatch  = (out.distance == 0 | (out.distance<=0 & out.distance>=-1));
idx_invalid  = (out.distance == -1 | out.distance == -8);
idx_watch    = out.distance > 0;
out.nowatch  = sum(idx_nowatch(:))*dt;
out.invalid  = sum(idx_invalid(:))*dt;
out.total    = length(out.distance)*dt;
out.watch    = sum(idx_watch(:))*dt; % out.total - out.nowatch - out.invalid;
return