function [Data] = CSVtoStruct_Unity(filename)
%% Load data
% This script turns the csv logging file into data structures.
% Author: Johnson Mok
% Last Updated: 17-12-2020

% disp('Start loading data...');
tic;

%% Info from logfile name
extracar            = check_diAV(filename);
name_split          = split(filename,'_');
idx_exp             = find(contains(name_split,'expdef'));
Data.expdefNr       = str2double(name_split{idx_exp+1});
idx_trial           = find(contains(name_split,'Trialnr'));
Data.trialNr        = str2double(name_split{idx_trial+1});
idx_par             = find(contains(name_split,'participant'));
Data.participantNr  = str2double(name_split{idx_par+1});
timescale_str       = name_split{idx_exp+2};
timescale_str_split = split(timescale_str,'-');
Data.timescale      = timescale_str_split{1};

%%
opts = delimitedTextImportOptions('Delimiter',';',...
                                  'DataLines', 10);
Loaded_Data = readmatrix(filename,opts);

%% Split data of type char from type double
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
M = Data_char;

%% Create Data structure
Data.sound.press     = M(:,10);
Data.sound.release   = M(:,11);
Data.resume          = M(:,12);
Data.dt              = M(1,1); 
Data.Time            = M(:,1);

% Data passenger and AV (3:54)
Data.pa.pos                      = struct3Coordinate(M(:,3:5));
Data.pa.rot                      = struct3Coordinate(M(:,6:8));
Data.pa.distance                 = M(:,13);
Data.pa.other.frame              = M(:,14);
Data.pa.other.captureTime        = M(:,15);
Data.pa.other.hmdpos             = struct3Coordinate(M(:,16:18));
Data.pa.other.hmdrot             = struct3Coordinate(M(:,19:21));
Data.pa.other.leftEyePupilSize   = M(:,22);
Data.pa.other.rightEyePupilSize  = M(:,23);
Data.pa.other.focusDistance      = M(:,24);
Data.pa.other.focusStability     = M(:,25);
Data.pa.HMD.gaze_dir             = struct3Coordinate(M(:,26:28));
Data.pa.world.gaze_dir           = struct3Coordinate(M(:,29:31));
Data.pa.HMD.gaze_origin          = struct3Coordinate(M(:,32:34));
Data.pa.world.gaze_origin        = struct3Coordinate(M(:,35:37));
Data.pa.local.v                  = struct3Coordinate(M(:,38:40));
Data.pa.local.v_smooth           = struct3Coordinate(M(:,41:43));
Data.pa.world.v                  = struct3Coordinate(M(:,44:46));
Data.pa.world.v_smooth           = struct3Coordinate(M(:,47:49));
Data.pa.world.rb_v               = struct3Coordinate(M(:,50:52));
Data.pa.local.rb_v               = struct3Coordinate(M(:,53:55));


if(extracar == true)
    % Data distraction AV if present (56:101)
    Data.diAV.pos                        = struct3Coordinate(M(:,56:58));
    Data.diAV.rot                        = struct3Coordinate(M(:,59:61));
    Data.diAV.blinkers                   = M(:,62);
    Data.diAV.press                      = M(:,63);
    Data.diAV.release                    = M(:,64);
    Data.diAV.distance                   = M(:,65);
    Data.diAV.other.frame                = M(:,66);
    Data.diAV.other.captureTime          = M(:,67);
    Data.diAV.other.hmdpos               = struct3Coordinate(M(:,68:70));
    Data.diAV.other.hmdrot               = struct3Coordinate(M(:,71:73));
    Data.diAV.other.leftEyePupilSize     = M(:,74);
    Data.diAV.other.rightEyePupilSize    = M(:,75);
    Data.diAV.other.focusDistance        = M(:,76);
    Data.diAV.other.focusStability       = M(:,77);
    Data.diAV.HMD.gaze_dir               = struct3Coordinate(M(:,78:80));
    Data.diAV.world.gaze_dir             = struct3Coordinate(M(:,81:83));
    Data.diAV.HMD.gaze_origin            = struct3Coordinate(M(:,84:86));
    Data.diAV.world.gaze_origin          = struct3Coordinate(M(:,87:89));
    Data.diAV.local.v                    = struct3Coordinate(M(:,90:92));
    Data.diAV.local.v_smooth             = struct3Coordinate(M(:,93:95));
    Data.diAV.world.v                    = struct3Coordinate(M(:,96:98));
    Data.diAV.world.v_smooth             = struct3Coordinate(M(:,99:101));

    % Data pedestrian when AV is present (101:132)
    Data.pe.pos                      = struct3Coordinate(M(:,102:104));
    Data.pe.rot                      = struct3Coordinate(M(:,105:107));
    Data.pe.distance                 = M(:,108);
    Data.pe.other.frame              = M(:,109);
    Data.pe.other.captureTime        = M(:,110);
    Data.pe.other.hmdpos             = struct3Coordinate(M(:,111:113));
    Data.pe.other.hmdrot             = struct3Coordinate(M(:,114:116));
    Data.pe.other.leftEyePupilSize   = M(:,117);
    Data.pe.other.rightEyePupilSize  = M(:,118);
    Data.pe.other.focusDistance      = M(:,119);
    Data.pe.other.focusStability     = M(:,120);
    Data.pe.HMD.gaze_dir             = struct3Coordinate(M(:,121:123));
    Data.pe.world.gaze_dir           = struct3Coordinate(M(:,124:126));
    Data.pe.HMD.gaze_origin          = struct3Coordinate(M(:,127:129));
    Data.pe.world.gaze_origin        = struct3Coordinate(M(:,130:132));
    Data.pe.gapAcceptance            = M(:,133);
end

if(extracar == false)
    % Data pedestrian when AV is absen (55:86)
    Data.diAV = nan;
    
    Data.pe.pos                      = struct3Coordinate(M(:,56:58));
    Data.pe.rot                      = struct3Coordinate(M(:,59:61));
    Data.pe.distance                 = M(:,62);
    Data.pe.other.frame              = M(:,63);
    Data.pe.other.captureTime        = M(:,64);
    Data.pe.other.hmdpos             = struct3Coordinate(M(:,65:67));
    Data.pe.other.hmdrot             = struct3Coordinate(M(:,68:70));
    Data.pe.other.leftEyePupilSize   = M(:,71);
    Data.pe.other.rightEyePupilSize  = M(:,72);
    Data.pe.other.focusDistance      = M(:,73);
    Data.pe.other.focusStability 	= M(:,74);
    Data.pe.HMD.gaze_dir             = struct3Coordinate(M(:,75:77));
    Data.pe.world.gaze_dir           = struct3Coordinate(M(:,78:80));
    Data.pe.HMD.gaze_origin          = struct3Coordinate(M(:,81:83));
    Data.pe.world.gaze_origin        = struct3Coordinate(M(:,84:86));
    Data.pe.gapAcceptance            = M(:,87);
end
%
t = toc;
disp(['Loading data took ', num2str(t) ,' seconds.']);
return