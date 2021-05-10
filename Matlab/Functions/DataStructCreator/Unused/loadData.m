function [Data, pa, pe, diAV, M] = loadData(filename, extracar)
%% Load data
% This script turns the csv logging file into data structures.
% Author: Johnson Mok

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

%% Data sound - needs to be put in the output too
sound.press     = M(:,10);
sound.release   = M(:,11);

% Data passenger and AV (3:54)
Data.dt                     = M(1,1); % in [s]
Data.Time                   = M(:,1);
pa.pos                      = struct3Coordinate(M(:,3:5));
pa.rot                      = struct3Coordinate(M(:,6:8));
pa.distance                 = M(:,12);
pa.other.frame              = M(:,13);
pa.other.captureTime        = M(:,14);
pa.other.hmdpos             = struct3Coordinate(M(:,15:17));
pa.other.hmdrot             = struct3Coordinate(M(:,18:20));
pa.other.leftEyePupilSize   = M(:,21);
pa.other.rightEyePupilSize  = M(:,22);
pa.other.focusDistance      = M(:,23);
pa.other.focusStability     = M(:,24);
pa.HMD.gaze_dir             = struct3Coordinate(M(:,25:27));
pa.world.gaze_dir           = struct3Coordinate(M(:,28:30));
pa.HMD.gaze_origin          = struct3Coordinate(M(:,31:33));
pa.world.gaze_origin        = struct3Coordinate(M(:,34:36));
pa.local.v                  = struct3Coordinate(M(:,37:39));
pa.local.v_smooth           = struct3Coordinate(M(:,40:42));
pa.world.v                  = struct3Coordinate(M(:,43:45));
pa.world.v_smooth           = struct3Coordinate(M(:,46:48));
pa.world.rb_v               = struct3Coordinate(M(:,49:51));
pa.local.rb_v               = struct3Coordinate(M(:,52:54));


if(extracar == true)
    % Data distraction AV if present (55:100)
    diAV.pos                        = struct3Coordinate(M(:,55:57));
    diAV.rot                        = struct3Coordinate(M(:,58:60));
    diAV.blinkers                   = M(:,61);
    diAV.press                      = M(:,62);
    diAV.release                    = M(:,63);
    diAV.distance                   = M(:,64);
    diAV.other.frame                = M(:,65);
    diAV.other.captureTime          = M(:,66);
    diAV.other.hmdpos               = struct3Coordinate(M(:,67:69));
    diAV.other.hmdrot               = struct3Coordinate(M(:,70:72));
    diAV.other.leftEyePupilSize     = M(:,73);
    diAV.other.rightEyePupilSize    = M(:,74);
    diAV.other.focusDistance        = M(:,75);
    diAV.other.focusStability       = M(:,76);
    diAV.HMD.gaze_dir               = struct3Coordinate(M(:,77:79));
    diAV.world.gaze_dir             = struct3Coordinate(M(:,80:82));
    diAV.HMD.gaze_origin            = struct3Coordinate(M(:,83:85));
    diAV.world.gaze_origin          = struct3Coordinate(M(:,86:88));
    diAV.local.v                    = struct3Coordinate(M(:,89:91));
    diAV.local.v_smooth             = struct3Coordinate(M(:,92:94));
    diAV.world.v                    = struct3Coordinate(M(:,95:97));
    diAV.world.v_smooth             = struct3Coordinate(M(:,98:100));

    % Data pedestrian when AV is present (101:132)
    pe.pos                      = struct3Coordinate(M(:,101:103));
    pe.rot                      = struct3Coordinate(M(:,104:106));
    pe.distance                 = M(:,107);
    pe.other.frame              = M(:,108);
    pe.other.captureTime        = M(:,109);
    pe.other.hmdpos             = struct3Coordinate(M(:,110:112));
    pe.other.hmdrot             = struct3Coordinate(M(:,113:115));
    pe.other.leftEyePupilSize   = M(:,116);
    pe.other.rightEyePupilSize  = M(:,117);
    pe.other.focusDistance      = M(:,118);
    pe.other.focusStability     = M(:,119);
    pe.HMD.gaze_dir             = struct3Coordinate(M(:,120:122));
    pe.world.gaze_dir           = struct3Coordinate(M(:,123:125));
    pe.HMD.gaze_origin          = struct3Coordinate(M(:,126:128));
    pe.world.gaze_origin        = struct3Coordinate(M(:,129:131));
    pe.gapAcceptance            = M(:,132);
end

if(extracar == false)
    % Data pedestrian when AV is absen (55:86)
    diAV = 0;
    
    pe.pos                      = struct3Coordinate(M(:,55:57));
    pe.rot                      = struct3Coordinate(M(:,58:60));
    pe.distance                 = M(:,61);
    pe.other.frame              = M(:,62);
    pe.other.captureTime        = M(:,63);
    pe.other.hmdpos             = struct3Coordinate(M(:,64:66));
    pe.other.hmdrot             = struct3Coordinate(M(:,67:69));
    pe.other.leftEyePupilSize   = M(:,70);
    pe.other.rightEyePupilSize  = M(:,71);
    pe.other.focusDistance      = M(:,72);
    pe.other.focusStability 	= M(:,73);
    pe.HMD.gaze_dir             = struct3Coordinate(M(:,74:76));
    pe.world.gaze_dir           = struct3Coordinate(M(:,77:79));
    pe.HMD.gaze_origin          = struct3Coordinate(M(:,80:82));
    pe.world.gaze_origin        = struct3Coordinate(M(:,83:85));
    pe.gapAcceptance            = M(:,86);
end
%
t = toc;
disp(['Loading data took ', num2str(t) ,' seconds.']);
return