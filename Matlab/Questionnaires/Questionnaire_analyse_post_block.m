%% Questionnaire analyser - Post-block
% This script reads in the questionnaire answers as a csv file and analyses 
% the data
% Author: Johnson Mok
% Last Updated: 07-01-2020
clc
close all
clear

%% Settings before running this script
ShowTables = true;
ShowPlots  = true;

%% Find questionnaire csv files
Folder   = join([cd,'\Questionnaire_Data']); %cd
FileList = dir(fullfile(Folder, '**', '*.csv'));

%% Add path to functions
addpath(genpath('Functions'));

%% Pre-experiment Questionnaire (nr 5 in FileList)
filename_pa     = join([FileList(1).folder,'\',FileList(1).name]);
filename_pe     = join([FileList(2).folder,'\',FileList(2).name]);
opts            = delimitedTextImportOptions('Delimiter',',','DataLines', 2);
Matrix_block_pa = readmatrix(filename_pa,opts);
Matrix_block_pa(7,:) = [];  % Removal wrong entry
Matrix_block_pe = readmatrix(filename_pe,opts);
Data_block_pa  = convertDataBlock_Pa(Matrix_block_pa);
Data_block_pe  = convertDataBlock_Pe(Matrix_block_pe);

%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%% Calculations %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%

%% Calulations - MISC
[MISC_pa, MISC_name_pa] = calcMisc(Data_block_pa.MISC);
[MISC_pe, MISC_name_pe] = calcMisc(Data_block_pe.MISC);

%% Calulations - Passenger
% Easy to direct the eye-gaze visualization
directLaser_pa = matchWithMapping(Data_block_pa.mapping, Data_block_pa.directLaser);
[directLaser_val_pa, directLaser_name_pa] = calcSevenLikertScaleMapping(directLaser_pa,'agree');

% Eye-gaze visualization distracting
laserDistract_pa = matchWithMapping(Data_block_pa.mapping, Data_block_pa.laserDistract);
[laserDistract_val_pa, laserDistract_name_pa] = calcSevenLikertScaleMapping(laserDistract_pa,'agree');

% Vehicle acted as predicted 
VAP_pa = matchWithMapping(Data_block_pa.mapping, Data_block_pa.vehicleActPredicted);
[VAP_val_pa, VAP_name_pa] = calcSevenLikertScaleMapping(VAP_pa,'easy');

%% Calulations - Pedestrian
% Mapping preference
prefMapping = matchWithMapping(Data_block_pa.mapping, Data_block_pe.prefMapping);
[prefMapping_val, prefMapping_name] = calcSevenLikertScaleMapping(prefMapping,'agree');

% Clear vehicle yielding 
clearVehicleYield = matchWithMapping(Data_block_pe.mapping, Data_block_pe.clearVehicleYield);
[clearVehicleYield_val, clearVehicleYield_name] = calcSevenLikertScaleMapping(clearVehicleYield,'agree');

%% Calulations - Acceptance
Acceptance_pa = AcceptanceVanDerLaan(Data_block_pa.mapping, Data_block_pa.acceptance);
Acceptance_pe = AcceptanceVanDerLaan(Data_block_pe.mapping, Data_block_pe.acceptance);
SPSS_pa_U = [Acceptance_pa.par.U0,Acceptance_pa.par.U1,Acceptance_pa.par.U2];
SPSS_pa_S = [Acceptance_pa.par.S0,Acceptance_pa.par.S1,Acceptance_pa.par.S2];
SPSS_pe_U = [Acceptance_pe.par.U0,Acceptance_pe.par.U1,Acceptance_pe.par.U2];
SPSS_pe_S = [Acceptance_pe.par.S0,Acceptance_pe.par.S1,Acceptance_pe.par.S2];
writematrix(SPSS_pa_U,'SPSS_pa_U.csv'); 
writematrix(SPSS_pa_S,'SPSS_pa_S.csv'); 
writematrix(SPSS_pe_U,'SPSS_pe_U.csv'); 
writematrix(SPSS_pe_S,'SPSS_pe_S.csv'); 

%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%% Comments %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
comments_pa = matchWithMapping(Data_block_pa.mapping, Data_block_pa.otherRemark);
comments_pe = matchWithMapping(Data_block_pa.mapping, Data_block_pe.otherRemark);

%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%% Plots %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
if(ShowPlots==true)
%% Plots Post-Block questionnaire - MISC
figure
% MISC score
X = categorical(MISC_name_pa);
X = reordercats(X,MISC_name_pa);
h = bar(X,[MISC_pa; MISC_pe]);
set(h, {'DisplayName'},{'passenger','pedestrian',}')
title('MISC Score'); ylabel('Number of participants'); legend();

%% Plots Post-Block questionnaire - Passenger
figure
% Easy to direct the eye-gaze visualization
subplot(2,3,1);
boxplot(directLaser_pa,'Labels',{'baseline','mapping 1','mapping 2'});
ylabel('Score'); title('Easy to direct the eye-gaze visualization.');
subplot(2,3,4);
X = categorical(directLaser_name_pa);
X = reordercats(X,directLaser_name_pa);
h = bar(X,directLaser_val_pa);
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
title('Easy to direct the eye-gaze visualization.'); ylabel('Number of participants'); legend();
% Eye-gaze visualization distracting
subplot(2,3,2);
boxplot(laserDistract_pa,'Labels',{'baseline','mapping 1','mapping 2'});
ylabel('Score'); title('Eye-gaze visualization distracting.');
subplot(2,3,5);
X = categorical(laserDistract_name_pa);
X = reordercats(X,laserDistract_name_pa);
h = bar(X,laserDistract_val_pa);
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
title('Eye-gaze visualization distracting.'); ylabel('Number of participants'); legend();
% Vehicle acted as predicted 
subplot(2,3,3);
boxplot(VAP_pa,'Labels',{'baseline','mapping 1','mapping 2'});
ylabel('Score'); title('The vehicle acted as predicted');
subplot(2,3,6);
X = categorical(VAP_name_pa);
X = reordercats(X,VAP_name_pa);
h = bar(X,VAP_val_pa);
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
title('The vehicle acted as predicted.'); ylabel('Number of participants'); legend();

%% Plots Post-Block questionnaire - Pedestrian
figure
% Prefer over baseline
subplot(2,2,1);
boxplot(prefMapping,'Labels',{'baseline','mapping 1','mapping 2'});
ylabel('Score'); title('You prefer the mapping over the baseline.');
subplot(2,2,3);
X = categorical(prefMapping_name);
X = reordercats(X,prefMapping_name);
h = bar(X,prefMapping_val);
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
title('You prefer the mapping over the baseline.'); ylabel('Number of participants'); legend();
% Clear vehicle yield
subplot(2,2,2);
boxplot(clearVehicleYield,'Labels',{'baseline','mapping 1','mapping 2'});
ylabel('Score'); title('It was clear to you when the vehicle was going to yield.');
subplot(2,2,4);
X = categorical(clearVehicleYield_name);
X = reordercats(X,clearVehicleYield_name);
h = bar(X,clearVehicleYield_val);
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
title('It was clear to you when the vehicle was going to yield.'); ylabel('Number of participants'); legend();

%% Plots Post-Block questionnaire - Acceptance
visualizeAcceptance(Acceptance_pe, Acceptance_pa);

end

%% Help functions Post-block Questionnaire
function data = convertDataBlock_Pa(input)
data.Date                = input(:,1);
data.participantNumber   = str2double(input(:,2));
data.mapping             = input(:,3);
data.MISC                = input(:,4);
data.directLaser         = str2double(input(:,5));
data.laserDistract       = str2double(input(:,6));
data.vehicleActPredicted = str2double(input(:,7));
data.otherRemark         = input(:,8);
% Acceptance
data.useful           = str2double(input(:,9));
data.pleasant         = str2double(input(:,10));
data.bad              = str2double(input(:,11));
data.nice             = str2double(input(:,12));
data.effective        = str2double(input(:,13));
data.irritating       = str2double(input(:,14));
data.assisting        = str2double(input(:,15));
data.undesirable      = str2double(input(:,16));
data.raisingAlertness = str2double(input(:,17));
data.acceptance       = str2double(input(:,9:17));
end
function data = convertDataBlock_Pe(input)
data.Date                = input(:,1);
data.participantNumber   = str2double(input(:,2));
data.mapping             = input(:,3);
data.MISC                = input(:,4);
data.prefMapping         = str2double(input(:,5));
data.clearVehicleYield   = str2double(input(:,6));
data.otherRemark         = input(:,7);
% Acceptance
data.useful           = str2double(input(:,8));
data.pleasant         = str2double(input(:,9));
data.bad              = str2double(input(:,10));
data.nice             = str2double(input(:,11));
data.effective        = str2double(input(:,12));
data.irritating       = str2double(input(:,13));
data.assisting        = str2double(input(:,14));
data.undesirable      = str2double(input(:,15));
data.raisingAlertness = str2double(input(:,16));
data.acceptance       = str2double(input(:,8:16));
end
function [outVal, name] = calcSevenLikertScaleMapping(data,string)
if(strcmp(string,'like'))
    name = {'Extremely unlikely';'Unlikely'; 'Bit unlikely'; 'Neutral';'bit likely';'Likely';'Extremely likely'};
elseif(strcmp(string,'agree'))
    name = {'Strongly disagree';'Disagree'; 'Slightly disagree'; 'Neutral';'Slightly agree';'Agree';'Strongly agree'};
elseif(strcmp(string,'easy'))
	name = {'Extremely difficult';'Difficult'; 'Slightly difficult'; 'Neutral';'Slightly easy';'Easy';'Extremely easy'};
end
outVal = zeros(size(data,2),length(name));
for j=1:size(data,2)
    [GC,GR] = groupcounts(data(:,j)); 
    val = zeros(1,length(name));
    for i=1:length(name)
        index = find(GR==i);
        if(~isempty(index))
            val(i) = GC(index);
        end
    end
    outVal(j,:) = val;
end
end
function [val, name] = calcMisc(data)
[GC,GR] = groupcounts(data); 
name = {'0 - No problems'; '1 - Uneasiness (no typical symptoms)';...
    '2 - Vague dizziness, warmth, headache, stomach awareness, sweating..';...
    '3 - Slight dizziness, warmth, headache, stomach awareness, sweating..';...
    '4 - Fairly dizziness, warmth, headache, stomach awareness, sweating..';...
    '5 - Severe dizziness, warmth, headache, stomach awareness, sweating..';...
    '6 - Slight naussea';...
    '7 - Fairly naussea';...
    '8 - Severe naussea';...
    '9 - (near) retching naussea';...
    '10 - Vomiting'};
val = zeros(1,length(name));
for i=1:length(name)
    index = find(strcmp(GR,name(i)));
    if(~isempty(index))
        val(i) = GC(index);
    end
end
end
function output = matchWithMapping(dataMap, data)
Idx0 = contains(dataMap,'Baseline');
Idx1 = contains(dataMap,'Mapping 1 (stop when looking)');
Idx2 = contains(dataMap,'Mapping 2 (stop when NOT looking');
Data_baseline = data(Idx0,:);
Data_map1     = data(Idx1,:);
Data_map2     = data(Idx2,:);
output = [Data_baseline, Data_map1, Data_map2];
end