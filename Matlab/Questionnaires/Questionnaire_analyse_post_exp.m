%% Questionnaire analyser - Post-experiment
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

% Find witmer table
path_witmer = join([cd,'\','Witmer_presence']);

%% Add path to functions
addpath(genpath('Functions_postexp'));
addpath(genpath('Functions_visualisation'));


%% Pre-experiment Questionnaire (nr 5 in FileList)
filename_pa     = join([FileList(3).folder,'\',FileList(3).name]);
filename_pe     = join([FileList(4).folder,'\',FileList(4).name]);
opts            = delimitedTextImportOptions('Delimiter',','); %,'DataLines', 0);
Matrix_pexp_pa  = readmatrix(filename_pa,opts);
Matrix_pexp_pe  = readmatrix(filename_pe,opts);
Data_pexp_pa    = convertDataPostExp(Matrix_pexp_pa,'passenger');
Data_pexp_pe    = convertDataPostExp(Matrix_pexp_pe,'pedestrian');

Witmer      = readcell(path_witmer);
Witmer(1,:) = [];

% Variables
mapNames = {'Baseline';'Gaze to Yield';'Look Away to Yield'};
%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%% Calculations %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%

%% Calculations - Passenger
% Instructions clear
[instrClear, instrClear_name] = calcSevenLikertScale(Data_pexp_pa.instructionsClear,'clear');
instClearMean = mean(Data_pexp_pa.instructionsClear);
instClearStd = std(Data_pexp_pa.instructionsClear);
%% Calculations - Preference mapping
[prefM_pa, prefM_name] = calcPreferenceMapping(Data_pexp_pa.prefMapAll);
[prefMIRL_pa, ~]       = calcPreferenceMapping(Data_pexp_pa.prefMapAll_IRL);
[prefM_pe, ~]          = calcPreferenceMapping(Data_pexp_pe.prefMapAll);
[prefMIRL_pe, ~]       = calcPreferenceMapping(Data_pexp_pe.prefMapAll_IRL);

rankMapping_pe = rankPreferenceMapping(Data_pexp_pe.prefMapAll);
rankMapping_pa = rankPreferenceMapping(Data_pexp_pa.prefMapAll);

rankMapping_pe_IRL = rankPreferenceMapping(Data_pexp_pe.prefMapAll_IRL);
rankMapping_pa_IRL = rankPreferenceMapping(Data_pexp_pa.prefMapAll_IRL);

%% Calculations presence Witmer
[pres_main_pa, pres_sub_pa, pres_name_pa, pres_mainStd_pa, pres_subStd_pa] = calcPresence(Data_pexp_pa.Presence, Witmer);
[pres_main_pe, pres_sub_pe, pres_name_pe, pres_mainStd_pe, pres_subStd_pe] = calcPresence(Data_pexp_pe.Presence, Witmer);


%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%% Plots %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%

%% Plots - Passenger
% figure
% % Instructions clear
% subplot(1,2,1);
% X = categorical(instrClear_name);
% X = reordercats(X,instrClear_name);
% bar(X,instrClear);
% title('Instructions clear'); ylabel('Percentage of answers');
% subplot(1,2,2);
% boxplot(Data_pexp_pa.instructionsClear);
% ylim([0.5 7.5]); 
% ylabel('7-Likert scale score'); title('Were the instructions clear?');

figure
VisBarError(instClearMean, instClearStd, {'all mappings'},'', {'Were the instructions clear?'})
yticks(1:7)
yticklabels({'extremely unclear','unclear,','slightly unclear','neutral','slightly clear','clear','extremely clear'});

%% Plots - Preference mapping
% figure
% % VR 
% subplot(2,2,1);
% X = categorical(prefM_name);
% X = reordercats(X,prefM_name);
% h = bar(X,prefM_pa');
% set(h, {'DisplayName'},mapNames);
% title('(VR) Preference mapping - Passenger'); ylabel('Percentage of choices'); legend();
% subplot(2,2,2);
% X = categorical(prefM_name);
% X = reordercats(X,prefM_name);
% h = bar(X,prefM_pa','stacked');
% set(h, {'DisplayName'},mapNames);
% title('(VR) Preference mapping - Passenger'); ylabel('Percentage of choices'); legend();
% subplot(2,2,3);
% X = categorical(prefM_name);
% X = reordercats(X,prefM_name);
% h = bar(X,prefM_pe');
% set(h, {'DisplayName'},mapNames);
% title('(VR) Preference mapping - Pedestrian'); ylabel('Percentage of choices'); legend();
% subplot(2,2,4);
% X = categorical(prefM_name);
% X = reordercats(X,prefM_name);
% h = bar(X,prefM_pe','stacked');
% set(h, {'DisplayName'},mapNames);
% title('(VR) Preference mapping - Pedestrian'); ylabel('Percentage of choices'); legend();
% 
% figure
% % IRL 
% subplot(2,2,1);
% X = categorical(prefM_name);
% X = reordercats(X,prefM_name);
% h = bar(X,prefMIRL_pa');
% set(h, {'DisplayName'},mapNames);
% title('(IRL) Preference mapping - Passenger'); ylabel('Percentage of choices'); legend();
% subplot(2,2,2);
% X = categorical(prefM_name);
% X = reordercats(X,prefM_name);
% h = bar(X,prefMIRL_pa','stacked');
% set(h, {'DisplayName'},mapNames);
% title('(IRL) Preference mapping - Passenger'); ylabel('Percentage of choices'); legend();
% subplot(2,2,3);
% X = categorical(prefM_name);
% X = reordercats(X,prefM_name);
% h = bar(X,prefMIRL_pe');
% set(h, {'DisplayName'},mapNames);
% title('(IRL) Preference mapping - Pedestrian'); ylabel('Percentage of choices'); legend();
% subplot(2,2,4);
% X = categorical(prefM_name);
% X = reordercats(X,prefM_name);
% h = bar(X,prefMIRL_pe','stacked');
% set(h, {'DisplayName'},mapNames);
% title('(IRL) Preference mapping - Pedestrian'); ylabel('Percentage of choices'); legend();

figure
subplot(1,2,1)
VisBarError(3-(rankMapping_pa(:,1)-1), rankMapping_pa(:,2), {'baseline','gaze to yield','look away to yield'},'Ranking', {'[Driver] ranking preference';'in experiment'})
yticks(1:3)
yticklabels({'3','2','1'});
ylim([0 3])
subplot(1,2,2)
VisBarError(3-(rankMapping_pe(:,1)-1), rankMapping_pe(:,2), {'baseline','gaze to yield','look away to yield'},'Ranking', {'[Pedestrian] ranking preference';'in experiment'})
yticks(1:3)
yticklabels({'3','2','1'});
ylim([0 3])

figure
subplot(1,2,1)
VisBarError(3-(rankMapping_pa_IRL(:,1)-1), rankMapping_pa_IRL(:,2), {'baseline','gaze to yield','look away to yield'},'Ranking', {'[Driver] ranking preference';'in real-life'})
yticks(1:3)
yticklabels({'3','2','1'});
ylim([0 3])
subplot(1,2,2)
VisBarError(3-(rankMapping_pe_IRL(:,1)-1), rankMapping_pe_IRL(:,2), {'baseline','gaze to yield','look away to yield'},'Ranking', {'[Pedestrian] ranking preference';'in real-life'})
yticks(1:3)
yticklabels({'3','2','1'});
ylim([0 3])


%% Plots - Presence
% figure
% % Main Scores
% X = categorical({'Passenger','Pedstrian'});
% X = reordercats(X,{'Passenger','Pedstrian'});
% h = bar(X, [pres_main_pa.all,pres_main_pa.Total;pres_main_pe.all,pres_main_pe.Total]);
% set(h, {'DisplayName'}, [pres_main_pa.name,'Total']');
% ylim([0.5 7.5]); grid on;
% title('Presence - main factors'); ylabel('7-Likert scale score'); legend();

figure
subplot(1,2,1)
VisBarError([pres_main_pa.all,pres_main_pa.Total],...
    [pres_mainStd_pa.all,pres_mainStd_pa.Total],{'CF','SF','DF','RF','NONE','Total'},...
    '7-Likert scale score',{'[Driver]','Presence - main factors'})
ylim([0 7]);
subplot(1,2,2)
VisBarError([pres_main_pe.all,pres_main_pe.Total],...
    [pres_mainStd_pe.all,pres_mainStd_pe.Total],{'CF','SF','DF','RF','NONE','Total'},...
    '7-Likert scale score',{'[Pedestrian]','Presence - main factors'})
ylim([0 7]);


figure
% Sub Scores
X = categorical({'Passenger','Pedstrian'});
X = reordercats(X,{'Passenger','Pedstrian'});
h = bar(X, [pres_sub_pa.all,pres_main_pa.Total;pres_sub_pe.all,pres_main_pe.Total]);
set(h, {'DisplayName'}, [pres_sub_pa.name,'Total']');
ylim([0.5 7.5]); grid on;
title('Presence - sub factors'); ylabel('7-Likert scale score'); legend();

figure % change main to sub
subplot(1,2,1)
VisBarError([pres_sub_pa.all,pres_main_pa.Total],...
    [pres_subStd_pa.all,pres_mainStd_pa.Total],{'INVC','NAT','AUD','HAPTIC','RES','IFQUAL','NONE','Total'},...
    '7-Likert scale score',{'[Driver]','Presence - sub factors'})
ylim([0 7]);
subplot(1,2,2)
VisBarError([pres_sub_pe.all,pres_main_pe.Total],...
    [pres_subStd_pe.all,pres_mainStd_pe.Total],{'INVC','NAT','AUD','HAPTIC','RES','IFQUAL','NONE','Total'},...
    '7-Likert scale score',{'[Pedestrian]','Presence - sub factors'})
ylim([0 7]);

% figure;
% % Boxplot main score - passenger
% subplot(1,2,1);
% boxplot([pres_main_pa.CFval, pres_main_pa.SFval, pres_main_pa.DFval, pres_main_pa.RFval, pres_main_pa.NONEval],...
%     [ones(size(pres_main_pa.CFval)), 2*ones(size(pres_main_pa.SFval)), 3*ones(size(pres_main_pa.DFval)), 4*ones(size(pres_main_pa.RFval)), 5*ones(size(pres_main_pa.NONEval))], ...
%     'Labels',{'CF','SF','DF','RF','NONE'});
% ylim([0.5 7.5]); grid on;
% ylabel('7-Likert scale score'); title('main score - passenger');
% % Boxplot main score - pedestrian
% subplot(1,2,2);
% boxplot([pres_main_pe.CFval, pres_main_pe.SFval, pres_main_pe.DFval, pres_main_pe.RFval, pres_main_pe.NONEval],...
%     [ones(size(pres_main_pe.CFval)), 2*ones(size(pres_main_pe.SFval)), 3*ones(size(pres_main_pe.DFval)), 4*ones(size(pres_main_pe.RFval)), 5*ones(size(pres_main_pe.NONEval))], ...
%     'Labels',{'CF','SF','DF','RF','NONE'});
% ylim([0.5 7.5]); grid on;
% ylabel('7-Likert scale score'); title('main score - pedestrian');
% 
% figure;
% % Boxplot sub score - passenger
% subplot(1,2,1);
% boxplot([pres_sub_pa.INVCval, pres_sub_pa.NATval, pres_sub_pa.AUDval, pres_sub_pa.HAPTICval, pres_sub_pa.RESval, pres_sub_pa.IFQUALval, pres_sub_pa.NONEval],...
%     [ones(size(pres_sub_pa.INVCval)), 2*ones(size(pres_sub_pa.NATval)), 3*ones(size(pres_sub_pa.AUDval)), 4*ones(size(pres_sub_pa.HAPTICval)), 5*ones(size(pres_sub_pa.RESval)), 6*ones(size(pres_sub_pa.IFQUALval)), 7*ones(size(pres_sub_pa.NONEval)),], ...
%     'Labels',{'INVC','NAT','AUD','HAPTIC','RES','IFQUAL','NONE'});
% ylim([0.5 7.5]); grid on; 
% ylabel('7-Likert scale score'); title('sub score - passenger');
% % Boxplot sub score - passenger
% subplot(1,2,2);
% boxplot([pres_sub_pe.INVCval, pres_sub_pe.NATval, pres_sub_pe.AUDval, pres_sub_pe.HAPTICval, pres_sub_pe.RESval, pres_sub_pe.IFQUALval, pres_sub_pe.NONEval],...
%     [ones(size(pres_sub_pe.INVCval)), 2*ones(size(pres_sub_pe.NATval)), 3*ones(size(pres_sub_pe.AUDval)), 4*ones(size(pres_sub_pe.HAPTICval)), 5*ones(size(pres_sub_pe.RESval)), 6*ones(size(pres_sub_pe.IFQUALval)), 7*ones(size(pres_sub_pe.NONEval)),], ...
%     'Labels',{'INVC','NAT','AUD','HAPTIC','RES','IFQUAL','NONE'});
% ylim([0.5 7.5]); grid on; 
% ylabel('7-Likert scale score'); title('sub score - pedestrian');


%% Helper functions Post-experiment Questionnaire
function data = convertDataPostExp(input,role)
i = 0;
data.title = input(1,:);
input(1,:)=[];
data.Date = input(1,:);
data.participantNumber = str2double(input(:,2));
if(strcmp(role,'passenger'))
    data.instructionsClear = str2double(input(:,3));
    i=1;
end
data.prefBaseline = input(:,3+i);
data.prefMap1 = input(:,4+i);
data.prefMap2 = input(:,5+i);
data.prefMapAll = input(:,(3+i):(5+i));
% Presence Witmer
data.PresenceName = data.title(6+i:37+i);
data.Presence = str2double(input(:,6+i:37+i));
% Preference IRL
if(any(strcmp(input(:,38+i),'Kolom 1')))
    input(:,(38+i):(40+i)) = replaceFaultyTitle(input(:,(38+i):(40+i)));
end
data.prefBaseline_IRL = input(:,38+i);
data.prefMap1_IRL = input(:,39+i);
data.prefMap2_IRL = input(:,40+i);
data.prefMapAll_IRL = input(:,(38+i):(40+i));
% Open questions
data.openQuestion1 = input(:,41+i);
data.openQuestion2 = input(:,42+i);
data.openQuestion3 = input(:,43+i);
end
function [outVal, name] = calcSevenLikertScale(data,string)
if(strcmp(string,'like'))
    name = {'Extremely unlikely';'Unlikely'; 'Bit unlikely'; 'Neutral';'bit likely';'Likely';'Extremely likely'};
elseif(strcmp(string,'agree'))
    name = {'Strongly disagree';'Disagree'; 'Slightly disagree'; 'Neutral';'Slightly agree';'Agree';'Strongly agree'};
elseif(strcmp(string,'easy'))
	name = {'Extremely difficult';'Difficult'; 'Slightly difficult'; 'Neutral';'Slightly easy';'Easy';'Extremely easy'};
elseif(strcmp(string,'clear'))
	name = {'Extremely unclear';'unclear'; 'Slightly unclear'; 'Neutral';'Slightly clear';'Clear';'Extremely clear'};
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
    outVal(j,:) = val*100/length(data);
end
end
function [outVal, name] = calcPreferenceMapping(data)
name = {'Most preferred','Second preferred','Least preferred'};
outVal = zeros(size(data,2),length(name));
for j=1:size(data,2)
    [GC,GR] = groupcounts(data(:,j)); 
    val = zeros(1,length(name));
    for i=1:length(name)
        index = find(strcmp(GR,name(i)));
        if(~isempty(index))
            val(i) = GC(index);
        end
    end
    outVal(j,:) = val*100/length(data);
end
end
function out = rankPreferenceMapping(data)
name = {'Most preferred','Second preferred','Least preferred'};
rank = zeros(size(data));
for i =1:length(name)
    rank(strcmp(data,name(i))) = i;
end
MeanRank = mean(rank);
StdRank = std(rank);
out = [MeanRank', StdRank'];
end
function output = replaceFaultyTitle(data)
idx = strcmp(data,'Kolom 1');
output = data;
output(idx) = {'Least preferred'};
end
function [imaj, isub, name] = categorizeWitmer(data)
% Major factors index
majorFactorCat = {'CF','SF','DF','RF','NONE'};
idx_mat = zeros(length(data),length(majorFactorCat));
for i=1:length(majorFactorCat)
    idx_mat(:,i) = contains(data(:,2),majorFactorCat(i));
end
imaj.CF   = find(idx_mat(:,1)==1);
imaj.SF   = find(idx_mat(:,2)==1);
imaj.DF   = find(idx_mat(:,3)==1);
imaj.RF   = find(idx_mat(:,4)==1);
imaj.NONE = find(idx_mat(:,5)==1);

% Subscales index
subscales = {'INV/C','NAT','AUD','HAPTIC','RES','IFQUAL','NONE'};
idx_sub = zeros(length(data),length(subscales));
for i=1:length(subscales)
    idx_sub(:,i) = contains(data(:,3),subscales(i));
end
isub.INVC   = find(idx_sub(:,1)==1);
isub.NAT   = find(idx_sub(:,2)==1);
isub.AUD   = find(idx_sub(:,3)==1);
isub.HAPTIC   = find(idx_sub(:,4)==1);
isub.RES = find(idx_sub(:,5)==1);
isub.IFQUAL = find(idx_sub(:,6)==1);
isub.NONE = find(idx_sub(:,7)==1);

% Questions
name.all = data(:,1);
end
function [main, sub, Q_name, mainStd, subStd] = calcPresence(data, Witmer)
[wit_imaj, wit_isub, wit_name] = categorizeWitmer(Witmer);
meanQuestion = mean(data);
main.Total = mean(meanQuestion);
mainStd.Total = std(meanQuestion);
% Major Factors
main.name = {'CF','SF','DF','RF','NONE'};
main.CF = mean(meanQuestion(wit_imaj.CF));
main.SF = mean(meanQuestion(wit_imaj.SF));
main.DF = mean(meanQuestion(wit_imaj.DF));
main.RF = mean(meanQuestion(wit_imaj.RF));
main.NONE = mean(meanQuestion(wit_imaj.NONE));
main.all = [main.CF, main.SF, main.DF, main.RF, main.NONE];

mainStd.CF = std(meanQuestion(wit_imaj.CF));
mainStd.SF = std(meanQuestion(wit_imaj.SF));
mainStd.DF = std(meanQuestion(wit_imaj.DF));
mainStd.RF = std(meanQuestion(wit_imaj.RF));
mainStd.NONE = std(meanQuestion(wit_imaj.NONE));
mainStd.all = [mainStd.CF, mainStd.SF, mainStd.DF, mainStd.RF, mainStd.NONE];

% Major Factors - separate values
main.CFval = meanQuestion(wit_imaj.CF);
main.SFval = meanQuestion(wit_imaj.SF);
main.DFval = meanQuestion(wit_imaj.DF);
main.RFval = meanQuestion(wit_imaj.RF);
main.NONEval = meanQuestion(wit_imaj.NONE);

% Subscales
sub.name = {'INVC','NAT','AUD','HAPTIC','RES','IFQUAL','NONE'};
sub.INVC = mean(meanQuestion(wit_isub.INVC));
sub.NAT = mean(meanQuestion(wit_isub.NAT));
sub.AUD = mean(meanQuestion(wit_isub.AUD));
sub.HAPTIC = mean(meanQuestion(wit_isub.HAPTIC));
sub.RES = mean(meanQuestion(wit_isub.RES));
sub.IFQUAL = mean(meanQuestion(wit_isub.IFQUAL));
sub.NONE = mean(meanQuestion(wit_isub.NONE));
sub.all = [sub.INVC, sub.NAT, sub.AUD, sub.HAPTIC, sub.RES, sub.IFQUAL, sub.NONE];

subStd.INVC = std(meanQuestion(wit_isub.INVC));
subStd.NAT = std(meanQuestion(wit_isub.NAT));
subStd.AUD = std(meanQuestion(wit_isub.AUD));
subStd.HAPTIC = std(meanQuestion(wit_isub.HAPTIC));
subStd.RES = std(meanQuestion(wit_isub.RES));
subStd.IFQUAL = std(meanQuestion(wit_isub.IFQUAL));
subStd.NONE = std(meanQuestion(wit_isub.NONE));
subStd.all = [subStd.INVC, subStd.NAT, subStd.AUD, subStd.HAPTIC, subStd.RES, subStd.IFQUAL, subStd.NONE];

% Subscales - separate values
sub.INVCval = meanQuestion(wit_isub.INVC);
sub.NATval = meanQuestion(wit_isub.NAT);
sub.AUDval = meanQuestion(wit_isub.AUD);
sub.HAPTICval = meanQuestion(wit_isub.HAPTIC);
sub.RESval = meanQuestion(wit_isub.RES);
sub.IFQUALval = meanQuestion(wit_isub.IFQUAL);
sub.NONEval = meanQuestion(wit_isub.NONE);
% Questions - main
Q_name.all = wit_name.all;
Q_name.main.CF = wit_name.all(wit_imaj.CF);
Q_name.main.SF = wit_name.all(wit_imaj.SF);
Q_name.main.DF = wit_name.all(wit_imaj.DF);
Q_name.main.RF = wit_name.all(wit_imaj.RF);
Q_name.main.NONE = wit_name.all(wit_imaj.NONE);
% Questions - sub
Q_name.sub.INVC = wit_name.all(wit_isub.INVC);
Q_name.sub.NAT = wit_name.all(wit_isub.NAT);
Q_name.sub.AUD = wit_name.all(wit_isub.AUD);
Q_name.sub.HAPTIC = wit_name.all(wit_isub.HAPTIC);
Q_name.sub.RES = wit_name.all(wit_isub.RES);
Q_name.sub.IFQUAL = wit_name.all(wit_isub.IFQUAL);
Q_name.sub.NONE = wit_name.all(wit_isub.INVC);
end






