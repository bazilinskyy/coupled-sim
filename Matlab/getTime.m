function output = getTime(distance, dt, display)
% Time stamps
t_start_tracking = find(distance==0, 1, 'first');
t_end_tracking = find(distance==0, 1, 'last');
t_start_gazing = find(distance>0, 1, 'first');
t_end_gazing = find(distance>0, 1, 'last');
time_total = length(distance)*dt;

% Phase 1: Eye-calibration
time1_total = dt*t_start_tracking;

% Phase 2: Tracking till AV stop
distance_2 = distance(t_start_tracking:t_end_gazing);
idx_2_0 = (distance_2 == 0);
idx_2_1 = (distance_2 == -1);
time2_nowatch = sum(idx_2_0(:))*dt;
time2_invalid = sum(idx_2_1(:))*dt;
time2_total = length(distance_2)*dt;
time2_watch = time2_total - time2_nowatch - time2_invalid;


% Phase 3: After reset
distance_3 = distance(t_end_gazing:t_end_tracking);
idx_3_0 = (distance_3 == 0);
idx_3_1 = (distance_3 == -1);
time3_nowatch = sum(idx_3_0(:))*dt;
time3_invalid = sum(idx_3_1(:))*dt;
time3_total = length(distance_3)*dt;
time3_watch = time3_total - time3_nowatch - time3_invalid;

% Table
Phase = {'Total'; 'Eye-calibration'; 'Tracking till pedestrian'; 'Tracking after pedestrian'};
Duration_total = [time_total; time1_total; time2_total; time3_total];
Duration_invalid = [nan; nan; time2_invalid; time3_invalid];
Duration_nowatch = [nan; nan; time2_nowatch; time3_nowatch];
Duration_watch = [nan; nan; time2_watch; time3_watch];
T = table(Phase, Duration_total, Duration_invalid, Duration_nowatch, Duration_watch);

% Display table
if(display==1)
    disp(T);
end

output = T;
return