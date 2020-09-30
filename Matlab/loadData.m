function Data = loadData(filename)
%% Load data
% Needs an update everytime the Worldlogging scripts is modified.
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
Data.distance = M(:,10);
Data.pos = struct3Coordinate(M(:,3:5));
Data.rot = struct3Coordinate(M(:,6:8));
Data.HMD.gaze_dir = struct3Coordinate(M(:,11:13));
Data.world.gaze_dir = struct3Coordinate(M(:,14:16));
Data.HMD.gaze_origin = struct3Coordinate(M(:,17:19));
Data.world.gaze_origin = struct3Coordinate(M(:,20:22));
Data.local.v = struct3Coordinate(M(:,23:25));
Data.local.v_smooth = struct3Coordinate(M(:,26:28));
Data.world.v = struct3Coordinate(M(:,29:31));
Data.world.v_smooth = struct3Coordinate(M(:,32:34));
Data.world.rb_v = struct3Coordinate(M(:,35:37));
Data.local.rb_v = struct3Coordinate(M(:,38:40));
t = toc;

disp(['Loading data took ', num2str(t) ,' seconds.']);
return