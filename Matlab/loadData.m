function [Data, pa, pe, diAV] = loadData(filename)
%% Load data
% Needs an update everytime the Worldlogging scripts is modified.
disp('Start loading data...');
tic;

opts = delimitedTextImportOptions('Delimiter',';',...
                                  'DataLines', 10);
Loaded_Data = readmatrix(filename,opts);

% Split data of type char from type double
is_char = zeros(1,size(Loaded_Data,2));
is_double = zeros(1,size(Loaded_Data,2));

for i=1:size(Loaded_Data,2)
    check = class(Loaded_Data{1,i});
    is_char(1,i) = strcmp(check, 'char');
    is_double(1,i) = strcmp(check, 'double');
end

Data_char = zeros(length(Loaded_Data),length(is_char));
for i=1:size(Loaded_Data,2)
    if is_char(i) == 1
        for j=1:size(Loaded_Data,1)
            Data_char(j,i) = str2double(Loaded_Data{j,i});
        end
    end
end

% Data_double = zeros(length(Data),length(is_double));
% n = 0;
% for i=1:size(Data,2)
%     if is_double(i) == 1
%         for j=1:size(Data,1)
%             n = n+1;
%             Data_double(j,n) = Data{j,i};
%         end
%     end
% end
% toc
M = Data_char;

%% Data passenger and AV
Data.dt = M(1,1); % in [s]
Data.Time = M(:,1);
pa.pos = struct3Coordinate(M(:,3:5));
pa.rot = struct3Coordinate(M(:,6:8));

pa.distance = M(:,10);
pa.other.frame = M(:,11);
pa.other.captureTime = M(:,12);
pa.other.hmdpos = struct3Coordinate(M(:,13:15));
pa.other.hmdrot = struct3Coordinate(M(:,16:18));
pa.other.leftEyePupilSize = M(:,19);
pa.other.rightEyePupilSize = M(:,20);
pa.other.focusDistance = M(:,21);
pa.other.focusStability = M(:,22);

pa.HMD.gaze_dir = struct3Coordinate(M(:,23:25));
pa.world.gaze_dir = struct3Coordinate(M(:,26:28));
pa.HMD.gaze_origin = struct3Coordinate(M(:,29:31));
pa.world.gaze_origin = struct3Coordinate(M(:,32:34));

pa.local.v = struct3Coordinate(M(:,35:37));
pa.local.v_smooth = struct3Coordinate(M(:,38:40));
pa.world.v = struct3Coordinate(M(:,41:43));
pa.world.v_smooth = struct3Coordinate(M(:,44:46));
pa.world.rb_v = struct3Coordinate(M(:,47:49));
pa.local.rb_v = struct3Coordinate(M(:,50:52));

%% Data distraction AV if present
%Need to write logic to determine whether AV is present
%Data.diAV = M(:,53:96);
diAV.pos = struct3Coordinate(M(:,53:55));
diAV.distance = M(:,60);
diAV.world.gaze_dir = M(:,78);

%% Data pedestrian (when AV is present)
pe.pos = struct3Coordinate(M(:,97:99));
pe.rot = struct3Coordinate(M(:,100:102));

pe.distance = M(:,103);
pe.distance2 = M(:,144);
pe.distance3 = M(:,177);

pe.other.frame = M(:,104);
pe.other.captureTime = M(:,105);
pe.other.hmdpos = struct3Coordinate(M(:,106:108));
pe.other.hmdrot = struct3Coordinate(M(:,109:111));
pe.other.leftEyePupilSize = M(:,112);
pe.other.rightEyePupilSize = M(:,113);
pe.other.focusDistance = M(:,114);
pe.other.focusStability = M(:,115);

pe.HMD.gaze_dir = struct3Coordinate(M(:,116:118));
pe.world.gaze_dir = struct3Coordinate(M(:,119:121));
pe.HMD.gaze_origin = struct3Coordinate(M(:,122:124));
pe.world.gaze_origin = struct3Coordinate(M(:,125:127));

pe.gapAcceptance = M(:,128);

%%
t = toc;

disp(['Loading data took ', num2str(t) ,' seconds.']);
return