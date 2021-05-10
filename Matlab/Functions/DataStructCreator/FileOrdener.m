%% File ordener.
% This script reads the title of the file and categorizes it according to
% the experiment definition. The logdata must be in a folder called
% 'Logdata'. The new ordened data will be put into a folder called
% 'LOG_ordened'.
% Author: Johnson Mok
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
Folder   = join([cd,'\Logdata']);
FileList = dir(fullfile(Folder, '**', '*.csv'));

%% Move files to destinated location
for i=1:length(FileList)
   filename         = FileList(i).name;
   foldername       = FileList(i).folder;
   fileMove         = join([FileList(i).folder, '\', FileList(i).name]);
   filename_split   = split(filename, '_');
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
