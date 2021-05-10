%% Questionnaire analyser - Post-block
% This script reads in the questionnaire answers as a csv file and analyses 
% the data
% Author: Johnson Mok
clc
close all
clear

%% Settings before running this script
ShowPlots  = true;

%% Find questionnaire csv files
Folder   = join([cd,'\Questionnaire_Data']); %cd
FileList = dir(fullfile(Folder, '**', '*.csv'));

%% Add path to functions
addpath(genpath('Functions_postblock'));
addpath(genpath('Functions_visualisation'));

%% Pre-experiment Questionnaire (nr 5 in FileList)
filename_pa     = join([FileList(1).folder,'\',FileList(1).name]);
filename_pe     = join([FileList(2).folder,'\',FileList(2).name]);
opts            = delimitedTextImportOptions('Delimiter',',','DataLines', 2);
Matrix_block_pa = readmatrix(filename_pa,opts);
Matrix_block_pa(7,:) = [];  % Removal wrong entry
Matrix_block_pe = readmatrix(filename_pe,opts);
Data_block_pa  = convertDataBlock_Pa(Matrix_block_pa);
Data_block_pe  = convertDataBlock_Pe(Matrix_block_pe);

%% Calculations
% MISC
misc_pa = MISC(Data_block_pa.mapping, Data_block_pa.MISC);
misc_pe = MISC(Data_block_pa.mapping, Data_block_pe.MISC);

% No Category
NoCat = NoCategoryPostBlock(Data_block_pa, Data_block_pe);

% Acceptance
Acceptance_pa = AcceptanceVanDerLaan(Data_block_pa.mapping, Data_block_pa.acceptance);
Acceptance_pe = AcceptanceVanDerLaan(Data_block_pe.mapping, Data_block_pe.acceptance);
    save('Acceptance_pa.mat','Acceptance_pa');
    save('Acceptance_pe.mat','Acceptance_pe');

    % Create csv for statistical analysis in SPSS
    SPSS_pa_U = [Acceptance_pa.par.U0,Acceptance_pa.par.U1,Acceptance_pa.par.U2];
    SPSS_pa_S = [Acceptance_pa.par.S0,Acceptance_pa.par.S1,Acceptance_pa.par.S2];
    SPSS_pe_U = [Acceptance_pe.par.U0,Acceptance_pe.par.U1,Acceptance_pe.par.U2];
    SPSS_pe_S = [Acceptance_pe.par.S0,Acceptance_pe.par.S1,Acceptance_pe.par.S2];
    writematrix(SPSS_pa_U,'SPSS_pa_U.csv'); 
    writematrix(SPSS_pa_S,'SPSS_pa_S.csv'); 
    writematrix(SPSS_pe_U,'SPSS_pe_U.csv'); 
    writematrix(SPSS_pe_S,'SPSS_pe_S.csv'); 
    
%% t-test
t_pe_U = pairedSamplesttest(SPSS_pe_U);
t_pe_S = pairedSamplesttest(SPSS_pe_S);
t_pa_U = pairedSamplesttest(SPSS_pa_U);
t_pa_S = pairedSamplesttest(SPSS_pa_S);


%% Cohen's D
D_pa_U = CohensD(SPSS_pa_U);
D_pa_S = CohensD(SPSS_pa_S);
D_pe_U = CohensD(SPSS_pe_U);
D_pe_S = CohensD(SPSS_pe_S);


%% Comments
comments_pa = matchWithMapping(Data_block_pa.mapping, Data_block_pa.otherRemark);
comments_pe = matchWithMapping(Data_block_pa.mapping, Data_block_pe.otherRemark);

%% Plots
if(ShowPlots==true)
visualizeMISC(misc_pa, misc_pe);
visualizeNoCategoryPostBlock(NoCat);
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
function output = matchWithMapping(dataMap, data)
Idx0 = contains(dataMap,'Baseline');
Idx1 = contains(dataMap,'Mapping 1 (stop when looking)');
Idx2 = contains(dataMap,'Mapping 2 (stop when NOT looking');
Data_baseline = data(Idx0,:);
Data_map1     = data(Idx1,:);
Data_map2     = data(Idx2,:);
output = [Data_baseline, Data_map1, Data_map2];
end
function out = pairedSamplesttest(data)
[~,p1,~,stats1] = ttest(data(:,1), data(:,2));
[~,p2,~,stats2] = ttest(data(:,2), data(:,3));
[~,p3,~,stats3] = ttest(data(:,1), data(:,3));
out = zeros(3,3);
out(1,:) = [stats1.tstat, stats1.df, p1];
out(2,:) = [stats2.tstat, stats2.df, p2];
out(3,:) = [stats3.tstat, stats3.df, p3];
end