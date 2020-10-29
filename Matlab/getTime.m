function output = getTime(distance, dt, display)
disp('Start calculating durations...');

% Time stamps
t.timeStamps.start_tracking = find(distance==0, 1, 'first');
t.timeStamps.end_tracking = find(distance==0, 1, 'last');
t.timeStamps.start_gazing = find(distance>0, 1, 'first');
t.timeStamps.end_gazing = find(distance>0, 1, 'last');
t.time_total = length(distance)*dt;

% Variables for time stamps
t_st = t.timeStamps.start_tracking;
t_et = t.timeStamps.end_tracking;
t_sg = t.timeStamps.start_gazing;
t_eg = t.timeStamps.end_gazing;
t_tt = t.time_total;

% Phase 1: Eye-calibration
t.time1.phase = "Eye-calibration";
t.time1.total = dt*t_st;

% Phase 2: Tracking till AV stop
t.time2.phase = "Tracking till pedestrian";
t.time2.distance = distance(t_st:t_eg);
idx_2_0 = (t.time2.distance == 0);
idx_2_1 = (t.time2.distance == -1);
t.time2.nowatch = sum(idx_2_0(:))*dt;
t.time2.invalid = sum(idx_2_1(:))*dt;
t.time2.total = length(t.time2.distance)*dt;
t.time2.watch = t.time2.total - t.time2.nowatch - t.time2.invalid;


% Phase 3: After reset
t.time3.phase = "Tracking after pedestrian";
t.time3.distance = distance(t_eg:t_et);
idx_3_0 = (t.time3.distance == 0);
idx_3_1 = (t.time3.distance == -1);
t.time3.nowatch = sum(idx_3_0(:))*dt;
t.time3.invalid = sum(idx_3_1(:))*dt;
t.time3.total = length(t.time3.distance)*dt;
t.time3.watch = t.time3.total - t.time3.nowatch - t.time3.invalid;

% Table
if(display==1)
    Phase = {'Total'; 'Eye-calibration'; 'Tracking till pedestrian'; 'Tracking after pedestrian'};
    Duration_total = [t.time_total; t.time1.total; t.time2.total; t.time3.total];
    Duration_invalid = [nan; nan; t.time2.invalid; t.time3.invalid];
    Duration_nowatch = [nan; nan; t.time2.nowatch; t.time3.nowatch];
    Duration_watch = [nan; nan; t.time2.watch; t.time3.watch];
    T = table(Phase, Duration_total, Duration_invalid, Duration_nowatch, Duration_watch);

    % Display table
    disp(T);
end

disp('Finished calculating durations');
output = t;
return