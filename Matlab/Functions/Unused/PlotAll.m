%% Data plotter
% This script plots the following data for one trial:
% -
% Author: Johnson Mok
% Last Updated: 21-12-2020

% Optional varargin to choose pedestrian, passenger, distraction vehicle or
% all to plot.
function PlotAll(AllData)
    fields_ED = fieldnames(AllData);
    for j = 1:length(fields_ED)
        fields_time = fieldnames(AllData.(fields_ED{j}));
        for k = 1:length(fields_time)
            fields_participants = fieldnames(AllData.(fields_ED{j}).(fields_time{k}));
            for idx = 1:length(fields_participants)
                fields_trials = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}));
                for i = 1:length(fields_trials)
                  funPlotData(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}), 'pedestrian');
                end
            end
        end
    end
end

function funPlotData(Data, varargin) 
%% Determine what to plot
% default value for varargins
ped = false;
pas = false;
diAV = false;
for i =1:length(varargin)
    if(strcmp(num2str(varargin{i}),'pedestrian'))
        ped = true;
    elseif(strcmp(num2str(varargin{i}),'passenger'))
        pas = true;
    elseif(strcmp(num2str(varargin{i}),'distraction'))
        diAV = true;
    elseif(strcmp(num2str(varargin{i}),'all'))
        ped = true;
        pas = true;
        diAV = true;
    else
        warning(['dataPlotter: ',num2str(varargin{i}), ' is not a valid input']);
    end
end

disp('Start plotting data...');

%% Plots for the passenger 
if(pas==true)
    % pos_xyz vs time
    figure;
    grid on;
    hold on;
    plot(Data.Time, Data.pa.pos.x, 'b');
    plot(Data.Time, Data.pa.pos.y, 'r');
    plot(Data.Time, Data.pa.pos.z, 'g');
    ylabel('Position of the passenger in world coordinates [m]');
    legend('x','y','z');
    title('Position passenger vs Time');

    % rot_xyz vs time
    figure;
    grid on;
    hold on;
    plot(Data.Time, Data.pa.rot.x, 'b');
    plot(Data.Time, Data.pa.rot.y, 'r');
    plot(Data.Time, Data.pa.rot.z, 'g');
    ylabel('Rotation of the passenger in world coordinates [m]');
    legend('x','y','z');
    title('Rotation passenger vs Time');

    % eye-gaze distance vs time
    figure;
    grid on;
    hold on;
    plot(Data.Time, Data.pa.distance);
    ylabel('Distance of passenger gaze with pedestrian [m]');
    title('Distance passenger gaze vs Time');

    % left and right eye pupilsize vs time
    figure;
    grid on;
    hold on;
    plot(Data.Time, Data.pa.other.leftEyePupilSize, 'b');
    plot(Data.Time, Data.pa.other.rightEyePupilSize, 'r');
    ylabel('Eye pupil sizse');
    legend('left','right');
    title('Eye pupil size passenger vs Time');

    % left and right focus vs time
    figure;
    grid on;
    hold on;
    plot(Data.Time, Data.pa.other.focusDistance, 'b');
    plot(Data.Time, Data.pa.other.focusStability, 'r');
    ylabel('Eye pupil sizse');
    legend('focus distance','focus stability');
    title('Focus passenger vs Time');

    % hmd gaze dir vs time
    figure;
    grid on;
    hold on;
    plot(Data.Time, Data.pa.HMD.gaze_dir.x, 'b');
    plot(Data.Time, Data.pa.HMD.gaze_dir.y, 'r');
    plot(Data.Time, Data.pa.HMD.gaze_dir.z, 'g');
    ylabel('HMD gaze direction');
    legend('x','y','z');
    title('HMD gaze direction vs Time');

    % hmd gaze origin vs time
    figure;
    grid on;
    hold on;
    plot(Data.Time, Data.pa.HMD.gaze_origin.x, 'b');
    plot(Data.Time, Data.pa.HMD.gaze_origin.y, 'r');
    plot(Data.Time, Data.pa.HMD.gaze_origin.z, 'g');
    ylabel('HMD origin direction');
    legend('x','y','z');
    title('HMD gaze origin vs Time');

    % world gaze dir vs time
    figure;
    grid on;
    hold on;
    plot(Data.Time, Data.pa.world.gaze_dir.x, 'b');
    plot(Data.Time, Data.pa.world.gaze_dir.y, 'r');
    plot(Data.Time, Data.pa.world.gaze_dir.z, 'g');
    ylabel('world gaze direction');
    legend('x','y','z');
    title('world gaze direction vs Time');

    % world gaze origin vs time
    figure;
    grid on;
    hold on;
    plot(Data.Time, Data.pa.world.gaze_origin.x, 'b');
    plot(Data.Time, Data.pa.world.gaze_origin.y, 'r');
    plot(Data.Time, Data.pa.world.gaze_origin.z, 'g');
    ylabel('world origin direction');
    legend('x','y','z');
    title('world gaze origin vs Time');

    % world rigid body velocity vs time
    figure;
    grid on;
    hold on;
    plot(Data.Time, Data.pa.world.rb_v.x, 'b');
    plot(Data.Time, Data.pa.world.rb_v.y, 'r');
    plot(Data.Time, Data.pa.world.rb_v.z, 'g');
    ylabel('World passenger vehicle velocity [m/s2]');
    legend('x','y','z');
    title('World passenger vehicle velocity vs Time');

    % local rigid body velocity vs time
    figure;
    grid on;
    hold on;
    plot(Data.Time, Data.pa.local.rb_v.x, 'b');
    plot(Data.Time, Data.pa.local.rb_v.y, 'r');
    plot(Data.Time, Data.pa.local.rb_v.z, 'g');
    ylabel('Local passenger vehicle velocity [m/s2]');
    legend('x','y','z');
    title('Local passenger vehicle velocity vs Time');
end

%% Plots for the pedestrian
if(ped==true)
%     % pos_xyz vs time
%     figure;
%     grid on;
%     hold on;
%     plot(Data.Time, Data.pe.pos.x, 'b');
%     plot(Data.Time, Data.pe.pos.y, 'r');
%     plot(Data.Time, Data.pe.pos.z, 'g');
%     ylabel('Position of the pedestrian in world coordinates [m]');
%     legend('x','y','z');
%     title('Position pedestrian vs Time');
%     
%     % rot_xyz vs time
%     figure;
%     grid on;
%     hold on;
%     plot(Data.Time, Data.pe.rot.x, 'b');
%     plot(Data.Time, Data.pe.rot.y, 'r');
%     plot(Data.Time, Data.pe.rot.z, 'g');
%     ylabel('Rotation of the pedestrian in world coordinates [m]');
%     legend('x','y','z');
%     title('Rotation pedestrian vs Time');
%     
%     % eye-gaze distance vs time
%     figure;
%     grid on;
%     hold on;
%     plot(Data.Time, Data.pe.distance);
%     ylabel('Distance of pedestrian gaze with the passenger [m]');
%     title('Distance pedestrian gaze vs Time');
%     
%     % left and right eye pupilsize vs time
%     figure;
%     grid on;
%     hold on;
%     plot(Data.Time, Data.pe.other.leftEyePupilSize, 'b');
%     plot(Data.Time, Data.pe.other.rightEyePupilSize, 'r');
%     ylabel('Eye pupil size');
%     legend('left','right');
%     title('Eye pupil size pedestrian vs Time');
% 
%     % left and right focus vs time
%     figure;
%     grid on;
%     hold on;
%     plot(Data.Time, Data.pe.other.focusDistance, 'b');
%     plot(Data.Time, Data.pe.other.focusStability, 'r');
%     ylabel('Eye pupil sizse');
%     legend('focus distance','focus stability');
%     title('Focus pedestrian vs Time');
%     
%     % hmd gaze dir vs time
%     figure;
%     grid on;
%     hold on;
%     plot(Data.Time, Data.pe.HMD.gaze_dir.x, 'b');
%     plot(Data.Time, Data.pe.HMD.gaze_dir.y, 'r');
%     plot(Data.Time, Data.pe.HMD.gaze_dir.z, 'g');
%     ylabel('HMD gaze direction');
%     legend('x','y','z');
%     title('HMD gaze direction pedestrian vs Time');
% 
%     % hmd gaze origin vs time
%     figure;
%     grid on;
%     hold on;
%     plot(Data.Time, Data.pe.HMD.gaze_origin.x, 'b');
%     plot(Data.Time, Data.pe.HMD.gaze_origin.y, 'r');
%     plot(Data.Time, Data.pe.HMD.gaze_origin.z, 'g');
%     ylabel('HMD origin direction');
%     legend('x','y','z');
%     title('HMD gaze origin pedestrian vs Time');
% 
%     % world gaze dir vs time
%     figure;
%     grid on;
%     hold on;
%     plot(Data.Time, Data.pe.world.gaze_dir.x, 'b');
%     plot(Data.Time, Data.pe.world.gaze_dir.y, 'r');
%     plot(Data.Time, Data.pe.world.gaze_dir.z, 'g');
%     ylabel('world gaze direction');
%     legend('x','y','z');
%     title('world gaze direction pedestrian vs Time');
% 
%     % world gaze origin vs time
%     figure;
%     grid on;
%     hold on;
%     plot(Data.Time, Data.pe.world.gaze_origin.x, 'b');
%     plot(Data.Time, Data.pe.world.gaze_origin.y, 'r');
%     plot(Data.Time, Data.pe.world.gaze_origin.z, 'g');
%     ylabel('world origin direction');
%     legend('x','y','z');
%     title('world gaze origin pedestrian vs Time');
    
    % gap acceptance vs time
    figure;
    grid on;
    hold on;
    plot(Data.Time, Data.pe.gapAcceptance);
    ylabel('Gap acceptance');
    title('Gap acceptance vs Time');
end

disp('Finished plotting data');
end