function [Data] = CSVtoStruct_simple(filename)
%% Load data
% This script turns the csv logging file into data structures.
% SIMPLE VERSION: without nested structures
% Author: Johnson Mok
% Last Updated: 28-12-2020

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
Data.dt              = M(1,1); 
Data.Time            = M(:,1);

% Data passenger and AV (3:54)
Data.pa_pos                      = struct3Coordinate(M(:,3:5));
Data.pa_rot                      = struct3Coordinate(M(:,6:8));
Data.pa_distance                 = M(:,12);
Data.pa_other_frame              = M(:,13);
Data.pa_other_captureTime        = M(:,14);
Data.pa_other_hmdpos             = struct3Coordinate(M(:,15:17));
Data.pa_other_hmdrot             = struct3Coordinate(M(:,18:20));
Data.pa_other_leftEyePupilSize   = M(:,21);
Data.pa_other_rightEyePupilSize  = M(:,22);
Data.pa_other_focusDistance      = M(:,23);
Data.pa_other_focusStability     = M(:,24);
Data.pa_HMD_gaze_dir             = struct3Coordinate(M(:,25:27));
Data.pa_world_gaze_dir           = struct3Coordinate(M(:,28:30));
Data.pa_HMD_gaze_origin          = struct3Coordinate(M(:,31:33));
Data.pa_world_gaze_origin        = struct3Coordinate(M(:,34:36));
Data.pa_local_v                  = struct3Coordinate(M(:,37:39));
Data.pa_local_v_smooth           = struct3Coordinate(M(:,40:42));
Data.pa_world_v                  = struct3Coordinate(M(:,43:45));
Data.pa_world_v_smooth           = struct3Coordinate(M(:,46:48));
Data.pa_world_rb_v               = struct3Coordinate(M(:,49:51));
Data.pa_local_rb_v               = struct3Coordinate(M(:,52:54));


if(extracar == true)
    % Data distraction AV if present (55:100)
    Data.diAV_pos                        = struct3Coordinate(M(:,55:57));
    Data.diAV_rot                        = struct3Coordinate(M(:,58:60));
    Data.diAV_blinkers                   = M(:,61);
    Data.diAV_press                      = M(:,62);
    Data.diAV_release                    = M(:,63);
    Data.diAV_distance                   = M(:,64);
    Data.diAV_other_frame                = M(:,65);
    Data.diAV_other_captureTime          = M(:,66);
    Data.diAV_other_hmdpos               = struct3Coordinate(M(:,67:69));
    Data.diAV_other_hmdrot               = struct3Coordinate(M(:,70:72));
    Data.diAV_other_leftEyePupilSize     = M(:,73);
    Data.diAV_other_rightEyePupilSize    = M(:,74);
    Data.diAV_other_focusDistance        = M(:,75);
    Data.diAV_other_focusStability       = M(:,76);
    Data.diAV_HMD_gaze_dir               = struct3Coordinate(M(:,77:79));
    Data.diAV_world_gaze_dir             = struct3Coordinate(M(:,80:82));
    Data.diAV_HMD_gaze_origin            = struct3Coordinate(M(:,83:85));
    Data.diAV_world_gaze_origin          = struct3Coordinate(M(:,86:88));
    Data.diAV_local_v                    = struct3Coordinate(M(:,89:91));
    Data.diAV_local_v_smooth             = struct3Coordinate(M(:,92:94));
    Data.diAV_world_v                    = struct3Coordinate(M(:,95:97));
    Data.diAV_world_v_smooth             = struct3Coordinate(M(:,98:100));

    % Data pedestrian when AV is present (101:132)
    Data.pe_pos                      = struct3Coordinate(M(:,101:103));
    Data.pe_rot                      = struct3Coordinate(M(:,104:106));
    Data.pe_distance                 = M(:,107);
    Data.pe_other_frame              = M(:,108);
    Data.pe_other_captureTime        = M(:,109);
    Data.pe_other_hmdpos             = struct3Coordinate(M(:,110:112));
    Data.pe_other_hmdrot             = struct3Coordinate(M(:,113:115));
    Data.pe_other_leftEyePupilSize   = M(:,116);
    Data.pe_other_rightEyePupilSize  = M(:,117);
    Data.pe_other_focusDistance      = M(:,118);
    Data.pe_other_focusStability     = M(:,119);
    Data.pe_HMD_gaze_dir             = struct3Coordinate(M(:,120:122));
    Data.pe_world_gaze_dir           = struct3Coordinate(M(:,123:125));
    Data.pe_HMD_gaze_origin          = struct3Coordinate(M(:,126:128));
    Data.pe_world_gaze_origin        = struct3Coordinate(M(:,129:131));
    Data.pe_gapAcceptance            = M(:,132);
end

if(extracar == false)
    % Data pedestrian when AV is absen (55:86)
    Data.diAV = nan;
    
    Data.pe_pos                      = struct3Coordinate(M(:,55:57));
    Data.pe_rot                      = struct3Coordinate(M(:,58:60));
    Data.pe_distance                 = M(:,61);
    Data.pe_other_frame              = M(:,62);
    Data.pe_other_captureTime        = M(:,63);
    Data.pe_other_hmdpos             = struct3Coordinate(M(:,64:66));
    Data.pe_other_hmdrot             = struct3Coordinate(M(:,67:69));
    Data.pe_other_leftEyePupilSize   = M(:,70);
    Data.pe_other_rightEyePupilSize  = M(:,71);
    Data.pe_other_focusDistance      = M(:,72);
    Data.pe_other_focusStability 	= M(:,73);
    Data.pe_HMD_gaze_dir             = struct3Coordinate(M(:,74:76));
    Data.pe_world_gaze_dir           = struct3Coordinate(M(:,77:79));
    Data.pe_HMD_gaze_origin          = struct3Coordinate(M(:,80:82));
    Data.pe_world_gaze_origin        = struct3Coordinate(M(:,83:85));
    Data.pe_gapAcceptance            = M(:,86);
end
%
t = toc;
disp(['Loading data took ', num2str(t) ,' seconds.']);
return