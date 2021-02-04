%% Get time
% This script calculates all the gazing times.
% Hierarchy: CalcTime -> getTime -> calcGazeTime
% Author: Johnson Mok
% Last Updated: 19-01-2020
% function output = getTime(distance, vel, dt, display)
function t = getTime(data, data_unp)
%% PASSENGER
% Assign variables
distance_unp = data_unp.pa.distance;
distance     = data.pa.distance;
pos          = data.pa.pos.z;
vel          = data.pa.world.rb_v.z;
dt           = data.dt;
display      = 0;
% Time stamps
t.TS_pa.startTrack_unp = find(distance_unp==0, 1, 'first');
t.TS_pa.startTrack     = find(distance==0, 1, 'first');
t.TS_pa.endTrack       = find(distance==0, 1, 'last');
t.TS_pa.startYield     = find(abs(vel)<0.1, 1, 'first');
t.TS_pa.endYield       = find(abs(vel)<0.1, 1, 'last');
if(isempty(t.TS_pa.startYield))
    t.TS_pa.startYield     = find(pos <= 23.43, 1, 'first');
    t.TS_pa.endYield       = find(pos <= 23.43, 1, 'first');
end
% Total run
t.time_total = length(distance)*dt;
t.pa.full = calcGazeTime(distance, dt, t.TS_pa.startTrack, t.TS_pa.endTrack);
% Phase 1: Eye-calibration time, useful in showing ease of use of
% eye-tracking. (Need unprocessed data)
t.pa.time1 = calcGazeTime(distance_unp, dt, 1, t.TS_pa.startTrack_unp);
% Phase 2: Tracking till AV stop
t.pa.time2 = calcGazeTime(distance, dt, t.TS_pa.startTrack, t.TS_pa.startYield);
% Phase 3: During yield
t.pa.time3 = calcGazeTime(distance, dt, t.TS_pa.startYield, t.TS_pa.endYield);
% Phase 4: After reset
t.pa.time4 = calcGazeTime(distance, dt, t.TS_pa.endYield, t.TS_pa.endTrack);
% Table
if(display==1)
    Phase_pa         = ["Total";   "Eye-calibration";   "Tracking till pedestrian";   "During yield";   "Tracking after pedestrian"];
    Duration_total   = [t.pa.full.total;   t.pa.time1.total;   t.pa.time2.total;   t.pa.time3.total;   t.pa.time4.total];
    Duration_invalid = [t.pa.full.invalid; t.pa.time1.invalid; t.pa.time2.invalid; t.pa.time3.invalid; t.pa.time4.invalid];
    Duration_nowatch = [t.pa.full.nowatch; t.pa.time1.nowatch; t.pa.time2.nowatch; t.pa.time3.nowatch; t.pa.time4.nowatch];
    Duration_watch   = [t.pa.full.watch;   t.pa.time1.watch;   t.pa.time2.watch;   t.pa.time3.watch;   t.pa.time4.watch ];
    T = table(Phase_pa, Duration_total, Duration_invalid, Duration_nowatch, Duration_watch);
    disp(T);
end

%% PASSENGER
% Assign variables
distance_unp2 = data_unp.pe.distance;
distance2     = data.pe.distance;
% Time stamps
t.TS_pe.startTrack_unp = find(distance_unp2<=0 & distance_unp2>=-1, 1, 'first');
t.TS_pe.startTrack     = find(distance2<=0 & distance2>=-1, 1, 'first');
t.TS_pe.endTrack       = find(distance2<=0 & distance2>=-1, 1, 'last');
% Total run
t.pe.full = calcGazeTime(distance2, dt, t.TS_pe.startTrack, t.TS_pe.endTrack);
% Phase 1: Eye-calibration time, useful in showing ease of use of
% eye-tracking. (Need unprocessed data)
t.pe.time1 = calcGazeTime(distance_unp2, dt, 1, t.TS_pe.startTrack_unp);
% Phase 2: Tracking till AV stop
t.pe.time2 = calcGazeTime(distance2, dt, t.TS_pe.startTrack, t.TS_pa.startYield);
% Phase 3: During yield
t.pe.time3 = calcGazeTime(distance2, dt, t.TS_pa.startYield, t.TS_pa.endYield);
% Phase 4: After reset
t.pe.time4 = calcGazeTime(distance2, dt, t.TS_pa.endYield, t.TS_pe.endTrack);
% Table
if(display==1)
    Phase_pe         = ["Total";   "Eye-calibration";   "Tracking till pedestrian";   "During yield";   "Tracking after pedestrian"];
    Duration_total   = [t.pe.full.total;   t.pe.time1.total;   t.pe.time2.total;   t.pe.time3.total;   t.pe.time4.total];
    Duration_invalid = [t.pe.full.invalid; t.pe.time1.invalid; t.pe.time2.invalid; t.pe.time3.invalid; t.pe.time4.invalid];
    Duration_nowatch = [t.pe.full.nowatch; t.pe.time1.nowatch; t.pe.time2.nowatch; t.pe.time3.nowatch; t.pe.time4.nowatch];
    Duration_watch   = [t.pe.full.watch;   t.pe.time1.watch;   t.pe.time2.watch;   t.pe.time3.watch;   t.pe.time4.watch ];
    T = table(Phase_pe, Duration_total, Duration_invalid, Duration_nowatch, Duration_watch);
    disp(T);
end

%% Passenger and Pedstrian
pa_watch = distance>0;
pe_watch = distance2>0;
t_eyeContact = pa_watch+pe_watch;
t.eyeContact = sum(t_eyeContact==2)*dt;
return

