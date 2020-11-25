function dataPlotter(Data, pa, pe, diAV)
disp('Start plotting data...');

%% Plots for the passenger 
% pos_xyz vs time
figure;
grid on;
hold on;
plot(Data.Time, pa.pos.x, 'b');
plot(Data.Time, pa.pos.y, 'r');
plot(Data.Time, pa.pos.z, 'g');
ylabel('Position of the passenger in world coordinates [m]');
legend('x','y','z');
title('Position passenger vs Time');

% distance_pa vs pos_AV
figure;
grid on;
yyaxis left
plot(Data.Time, pa.distance);
ylim([-10 100]);
ylabel("Distance_{Eye-gaze passenger} in [m]");
yyaxis right
plot(Data.Time, pa.pos.z);
ylim([-10 100]);
ylabel("AV pos_z in [m]");
xlabel('Time in [s]'); 
title("Passenger distance vs AV position");

% Left and right eye pupil size vs Time
figure;
grid on;
hold on;
plot(Data.Time, pa.other.leftEyePupilSize, 'b');
plot(Data.Time, pa.other.rightEyePupilSize, 'r');
ylabel('Eye pupil size [m]');
legend('left','right');
title('Eye pupil size vs Time');

% Focus distance and stability vs Time
figure;
grid on;
yyaxis left
plot(Data.Time, pa.other.focusDistance);
ylabel("Focus distance in [m]");                % Value between 0-2 metres.
yyaxis right
plot(Data.Time, pa.other.focusStability);
ylabel("Focus stability");                      % 0.0 means not stable 1.0 means stable. 
xlabel('Time in [s]'); 
title("Focus vs AV position");

%% Plots for the distraction AV
% Pos diAV and pa vs Time
% figure;
% grid on;
% hold on;
% plot(Data.Time, pa.pos.z, 'b');
% plot(Data.Time, diAV.pos.z, 'r');
% ylabel('Position on z in [m]');
% legend('Passenger','diAV');
% title('Position z vs Time');

%% Plots for the pedestrian 
figure;
grid on;
yyaxis left
plot(Data.Time, pe.distance);
ylim([-10 100]);
ylabel("Distance eye-gaze passenger in [m]");
yyaxis right
plot(Data.Time, pa.pos.z);
ylim([-10 100]);
ylabel("AV pos_z in [m]");
title("Pedestrian distance vs AV position");

% % Only for debugging
% figure;
% grid on;
% title("Distances");
% yyaxis left
% plot(Data.Time, pe.distance2);
% ylabel("eye gaze distance pedestrian 2");
% yyaxis right
% plot(Data.Time, pa.pos.z);
% ylim([-10 100]);
% ylabel("AV pos_z");
% 
% % Only for debugging
% figure;
% grid on;
% title("Distances");
% yyaxis left
% plot(Data.Time, pe.distance3);
% ylabel("eye gaze distance pedestrian 3");
% yyaxis right
% plot(Data.Time, pa.pos.z);
% ylim([-10 100]);
% ylabel("AV pos_z");

% Pedestrian gap acceptance vs AV position
figure;
grid on;
title("Gap Acceptance vs Distances");
yyaxis left
plot(Data.Time, pe.gapAcceptance);
ylabel("Gap acceptance");
yyaxis right
plot(Data.Time, pa.pos.z);
ylim([-10 100]);
ylabel("AV pos_z");

%% Combinations
% distance_pa vs distance_pe
figure;
grid on;
yyaxis left
plot(Data.Time, pa.distance);
ylabel("Distance_{Eye-gaze passenger} in [m]");
ylim([-10 50]);
yyaxis right
plot(Data.Time, pe.distance);
ylabel("Distance_{Eye-gaze pedestrian} in [m]");
ylim([-10 50]);
xlabel('Time in [s]'); 
title("Passenger distance vs AV Pedestrian distance");

%%
disp('Finished plotting data');
end