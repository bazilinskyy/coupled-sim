function [Data, pa, pe, diAV, M] = loadData(filename, extracar)
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

%% Data sound
sound.press = M(:,10);
sound.release = M(:,11);

%% Data passenger and AV
Data.dt = M(1,1); % in [s]
Data.Time = M(:,1);
pa.pos = struct3Coordinate(M(:,3:5));
pa.rot = struct3Coordinate(M(:,6:8));

pa.distance = M(:,12);
pa.other.frame = M(:,13);
pa.other.captureTime = M(:,14);
pa.other.hmdpos = struct3Coordinate(M(:,15:17));
pa.other.hmdrot = struct3Coordinate(M(:,18:20));
pa.other.leftEyePupilSize = M(:,21);
pa.other.rightEyePupilSize = M(:,22);
pa.other.focusDistance = M(:,23);
pa.other.focusStability = M(:,24);

pa.HMD.gaze_dir = struct3Coordinate(M(:,25:27));
pa.world.gaze_dir = struct3Coordinate(M(:,28:30));
pa.HMD.gaze_origin = struct3Coordinate(M(:,31:33));
pa.world.gaze_origin = struct3Coordinate(M(:,34:36));

pa.local.v = struct3Coordinate(M(:,37:39));
pa.local.v_smooth = struct3Coordinate(M(:,40:42));
pa.world.v = struct3Coordinate(M(:,43:45));
pa.world.v_smooth = struct3Coordinate(M(:,46:48));
pa.world.rb_v = struct3Coordinate(M(:,49:51));
pa.local.rb_v = struct3Coordinate(M(:,52:54));

%% Data distraction AV if present
%Need to write logic to determine whether AV is present
if(extracar == true)
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
end

%% Data pedestrian (when AV is absent)
if(extracar == false)
    diAV = 0;
pe.pos = struct3Coordinate(M(:,55:57));
pe.rot = struct3Coordinate(M(:,58:60));

pe.distance = M(:,61);

pe.other.frame = M(:,62);
pe.other.captureTime = M(:,63);
pe.other.hmdpos = struct3Coordinate(M(:,64:66));
pe.other.hmdrot = struct3Coordinate(M(:,67:69));
pe.other.leftEyePupilSize = M(:,70);
pe.other.rightEyePupilSize = M(:,71);
pe.other.focusDistance = M(:,72);
pe.other.focusStability = M(:,73);

pe.HMD.gaze_dir = struct3Coordinate(M(:,74:76));
pe.world.gaze_dir = struct3Coordinate(M(:,77:79));
pe.HMD.gaze_origin = struct3Coordinate(M(:,80:82));
pe.world.gaze_origin = struct3Coordinate(M(:,83:85));

pe.gapAcceptance = M(:,86);
end
%%
t = toc;

disp(['Loading data took ', num2str(t) ,' seconds.']);
return