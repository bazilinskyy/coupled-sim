%% File ordener.
% This script reads the title of the file and categorizes it according to the experiment definition; 
% Author: Johnson Mok
% Last Updated: 17-12-2020
clc
clear
disp('Start ordening data...');
tic;

%% Create folder structure
name_mainF = 'LOG_ordened';
if(~exist(name_mainF, 'dir'))       % Create main folder
    mkdir(name_mainF)
end
mainF = fullfile(pwd,name_mainF);   % Create string to main folder
ExpDef_dir = cell(12,1);            % Create exp definition folders
for i = 1:12
    ExpDef = {join(['ED_',num2str(i-1)])};
    ExpDef_dir(i,:) = fullfile(mainF, ExpDef);
        if(~exist(ExpDef_dir{i}, 'dir'))
            mkdir(ExpDef_dir{i})
        end
end

%% Get csv files from location 
%Get the directory contents
directory = 'Logdata';
dirc = dir(directory);
%Filter out all the folders.
dirc = dirc(find(~cellfun(@isdir,{dirc(:).name})));
for i=1:length(dirc)
   filename         = dirc(i).name;
   foldername       = dirc(i).folder;
   fileMove         = join([dirc(i).folder, '\', dirc(i).name]);
   filename_split   = split(filename, '_');
%    Participantnr = str2double(filename_split(2));
%    Trialnr = str2double(filename_split(4));
   ExpDefnr = str2double(filename_split(6));
   for j=0:11
        if(ExpDefnr==j)
            movefile(fileMove,ExpDef_dir{j+1});
        end
   end
end

%%
t = toc;
disp(['Ordening data took ', num2str(t) ,' seconds.']);
