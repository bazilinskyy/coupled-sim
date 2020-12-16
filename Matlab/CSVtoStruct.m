function [Data] = CSVtoStruct(filename, extracar)
%% Load data
% This script turns the csv logging file into data structures.
% Author: Johnson Mok
% Last Updated: 16-12-2020

disp('Start loading data...');
tic;
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
Data.pa.pos                      = struct3Coordinate(M(:,3:5));
Data.pa.rot                      = struct3Coordinate(M(:,6:8));
Data.pa.distance                 = M(:,12);
Data.pa.other.frame              = M(:,13);
Data.pa.other.captureTime        = M(:,14);
Data.pa.other.hmdpos             = struct3Coordinate(M(:,15:17));
Data.pa.other.hmdrot             = struct3Coordinate(M(:,18:20));
Data.pa.other.leftEyePupilSize   = M(:,21);
Data.pa.other.rightEyePupilSize  = M(:,22);
Data.pa.other.focusDistance      = M(:,23);
Data.pa.other.focusStability     = M(:,24);
Data.pa.HMD.gaze_dir             = struct3Coordinate(M(:,25:27));
Data.pa.world.gaze_dir           = struct3Coordinate(M(:,28:30));
Data.pa.HMD.gaze_origin          = struct3Coordinate(M(:,31:33));
Data.pa.world.gaze_origin        = struct3Coordinate(M(:,34:36));
Data.pa.local.v                  = struct3Coordinate(M(:,37:39));
Data.pa.local.v_smooth           = struct3Coordinate(M(:,40:42));
Data.pa.world.v                  = struct3Coordinate(M(:,43:45));
Data.pa.world.v_smooth           = struct3Coordinate(M(:,46:48));
Data.pa.world.rb_v               = struct3Coordinate(M(:,49:51));
Data.pa.local.rb_v               = struct3Coordinate(M(:,52:54));


if(extracar == true)
    % Data distraction AV if present (55:100)
    Data.diAV.pos                        = struct3Coordinate(M(:,55:57));
    Data.diAV.rot                        = struct3Coordinate(M(:,58:60));
    Data.diAV.blinkers                   = M(:,61);
    Data.diAV.press                      = M(:,62);
    Data.diAV.release                    = M(:,63);
    Data.diAV.distance                   = M(:,64);
    Data.diAV.other.frame                = M(:,65);
    Data.diAV.other.captureTime          = M(:,66);
    Data.diAV.other.hmdpos               = struct3Coordinate(M(:,67:69));
    Data.diAV.other.hmdrot               = struct3Coordinate(M(:,70:72));
    Data.diAV.other.leftEyePupilSize     = M(:,73);
    Data.diAV.other.rightEyePupilSize    = M(:,74);
    Data.diAV.other.focusDistance        = M(:,75);
    Data.diAV.other.focusStability       = M(:,76);
    Data.diAV.HMD.gaze_dir               = struct3Coordinate(M(:,77:79));
    Data.diAV.world.gaze_dir             = struct3Coordinate(M(:,80:82));
    Data.diAV.HMD.gaze_origin            = struct3Coordinate(M(:,83:85));
    Data.diAV.world.gaze_origin          = struct3Coordinate(M(:,86:88));
    Data.diAV.local.v                    = struct3Coordinate(M(:,89:91));
    Data.diAV.local.v_smooth             = struct3Coordinate(M(:,92:94));
    Data.diAV.world.v                    = struct3Coordinate(M(:,95:97));
    Data.diAV.world.v_smooth             = struct3Coordinate(M(:,98:100));

    % Data pedestrian when AV is present (101:132)
    Data.pe.pos                      = struct3Coordinate(M(:,101:103));
    Data.pe.rot                      = struct3Coordinate(M(:,104:106));
    Data.pe.distance                 = M(:,107);
    Data.pe.other.frame              = M(:,108);
    Data.pe.other.captureTime        = M(:,109);
    Data.pe.other.hmdpos             = struct3Coordinate(M(:,110:112));
    Data.pe.other.hmdrot             = struct3Coordinate(M(:,113:115));
    Data.pe.other.leftEyePupilSize   = M(:,116);
    Data.pe.other.rightEyePupilSize  = M(:,117);
    Data.pe.other.focusDistance      = M(:,118);
    Data.pe.other.focusStability     = M(:,119);
    Data.pe.HMD.gaze_dir             = struct3Coordinate(M(:,120:122));
    Data.pe.world.gaze_dir           = struct3Coordinate(M(:,123:125));
    Data.pe.HMD.gaze_origin          = struct3Coordinate(M(:,126:128));
    Data.pe.world.gaze_origin        = struct3Coordinate(M(:,129:131));
    Data.pe.gapAcceptance            = M(:,132);
end

if(extracar == false)
    % Data pedestrian when AV is absen (55:86)
    Data.diAV = nan;
    
    Data.pe.pos                      = struct3Coordinate(M(:,55:57));
    Data.pe.rot                      = struct3Coordinate(M(:,58:60));
    Data.pe.distance                 = M(:,61);
    Data.pe.other.frame              = M(:,62);
    Data.pe.other.captureTime        = M(:,63);
    Data.pe.other.hmdpos             = struct3Coordinate(M(:,64:66));
    Data.pe.other.hmdrot             = struct3Coordinate(M(:,67:69));
    Data.pe.other.leftEyePupilSize   = M(:,70);
    Data.pe.other.rightEyePupilSize  = M(:,71);
    Data.pe.other.focusDistance      = M(:,72);
    Data.pe.other.focusStability 	= M(:,73);
    Data.pe.HMD.gaze_dir             = struct3Coordinate(M(:,74:76));
    Data.pe.world.gaze_dir           = struct3Coordinate(M(:,77:79));
    Data.pe.HMD.gaze_origin          = struct3Coordinate(M(:,80:82));
    Data.pe.world.gaze_origin        = struct3Coordinate(M(:,83:85));
    Data.pe.gapAcceptance            = M(:,86);
end
%
t = toc;
disp(['Loading data took ', num2str(t) ,' seconds.']);
return