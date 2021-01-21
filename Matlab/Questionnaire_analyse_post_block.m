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
Acceptance_pa = calcAcceptance(Data_block_pa.mapping, Data_block_pa.acceptance);
Acceptance_pe = calcAcceptance(Data_block_pe.mapping, Data_block_pe.acceptance);

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
figure
% acceptance - passenger
subplot(1,2,1);
X = categorical(Acceptance_pa.Laan0.name);
X = reordercats(X,Acceptance_pa.Laan0.name);
h = plot(X,[Acceptance_pa.Laan0.meanAcc; Acceptance_pa.Laan1.meanAcc; Acceptance_pa.Laan2.meanAcc]);
y0 = yline(Acceptance_pa.Laan0.meanmean,'--','Baseline','LineWidth',2); y0.Color = [0, 0.4470, 0.7410]; 
y1 = yline(Acceptance_pa.Laan1.meanmean,'--','Mapping 1','LineWidth',2); y1.Color = [0.8500, 0.3250, 0.0980];
y2 = yline(Acceptance_pa.Laan2.meanmean,'--','Mapping 2','LineWidth',2); y2.Color = [0.9290, 0.6940, 0.1250];
grid on;
ylim([-2 2]);
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
title('Acceptance score - Passenger'); ylabel('Score'); legend();
% acceptance - pedestrian
subplot(1,2,2);
X = categorical(Acceptance_pe.Laan0.name);
X = reordercats(X,Acceptance_pe.Laan0.name);
h = plot(X,[Acceptance_pe.Laan0.meanAcc; Acceptance_pe.Laan1.meanAcc; Acceptance_pe.Laan2.meanAcc]);
y0 = yline(Acceptance_pe.Laan0.meanmean,'--','Baseline','LineWidth',2); y0.Color = [0, 0.4470, 0.7410];
y1 = yline(Acceptance_pe.Laan1.meanmean,'--','Mapping 1','LineWidth',2); y1.Color = [0.8500, 0.3250, 0.0980];
y2 = yline(Acceptance_pe.Laan2.meanmean,'--','Mapping 2','LineWidth',2); y2.Color = [0.9290, 0.6940, 0.1250];
grid on;
ylim([-2 2]);
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
title('Acceptance score - Pedestrian'); ylabel('Score'); legend();

figure
% Usefulness score 
subplot(2,1,1);
X = categorical({'Passenger','Pedestrian'});
X = reordercats(X,{'Passenger','Pedestrian'});
h = bar(X,[Acceptance_pa.usefulness_score0 Acceptance_pe.usefulness_score0;...
            Acceptance_pa.usefulness_score1 Acceptance_pe.usefulness_score1;...
            Acceptance_pa.usefulness_score2 Acceptance_pe.usefulness_score2]);
grid on;
ylim([-2 2]);
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
title('Usefulness'); ylabel('Score'); legend();
% satisfying score 
subplot(2,1,2);
X = categorical({'Passenger','Pedestrian'});
X = reordercats(X,{'Passenger','Pedestrian'});
h = bar(X,[Acceptance_pa.satisfying_score0 Acceptance_pe.satisfying_score0;...
            Acceptance_pa.satisfying_score1 Acceptance_pe.satisfying_score1;...
            Acceptance_pa.satisfying_score2 Acceptance_pe.satisfying_score2]);
grid on;
ylim([-2 2]);
set(h, {'DisplayName'},{'baseline','mapping 1','mapping 2'}')
title('Satisfying'); ylabel('Score'); legend();

% Plot with usefulness against satisfying
figure;
hold on;
X = [Acceptance_pa.satisfying_score0, Acceptance_pa.satisfying_score1, Acceptance_pa.satisfying_score2, Acceptance_pe.satisfying_score0, Acceptance_pe.satisfying_score1, Acceptance_pe.satisfying_score2];
Y = [Acceptance_pa.usefulness_score0, Acceptance_pa.usefulness_score1, Acceptance_pa.usefulness_score2, Acceptance_pe.usefulness_score0, Acceptance_pe.usefulness_score1, Acceptance_pe.usefulness_score2];
labels = {'Passenger - baseline','Passenger - mapping 1','Passenger - mapping 2','Pedestrian - baseline','Pedestrian - mapping 1','Pedestrian - mapping 2'};
for i=1:length(X)
    p = plot(X(i),Y(i),'o','MarkerSize',10);
    c = get(p,'Color');
    p.MarkerFaceColor = c;
end
text(X,Y,labels,'FontSize',14,'VerticalAlignment','bottom','HorizontalAlignment','right')
xlabel('Satisfying','FontSize',15,'FontWeight','bold'); xlim([-2 2]); xline(0,'--');
ylabel('Usefulness','FontSize',15,'FontWeight','bold'); ylim([-2 2]); yline(0,'--');
ax=gca; ax.FontSize = 20;
grid on;

figure
% Boxplot acceptance items passenger
subplot(3,2,1);
boxplot(Acceptance_pa.acc_0,'Labels',Acceptance_pa.Laan0.name);
ylabel('Score'); title('Acceptance score Baseline - Passenger.');
ylim([-2.5 2.5]);
subplot(3,2,3);
boxplot(Acceptance_pa.acc_1,'Labels',Acceptance_pa.Laan1.name);
ylabel('Score'); title('Acceptance score Mapping 1 - Passenger.');
ylim([-2.5 2.5]);
subplot(3,2,5);
boxplot(Acceptance_pa.acc_2,'Labels',Acceptance_pa.Laan2.name);
ylabel('Score'); title('Acceptance score Mapping 2 - Passenger.');
ylim([-2.5 2.5]);
% Boxplot acceptance items pedestrian
subplot(3,2,2);
boxplot(Acceptance_pe.acc_0,'Labels',Acceptance_pe.Laan0.name);
ylabel('Score'); title('Acceptance score Baseline - Pedestrian.');
ylim([-2.5 2.5]);
subplot(3,2,4);
boxplot(Acceptance_pe.acc_1,'Labels',Acceptance_pe.Laan1.name);
ylabel('Score'); title('Acceptance score Mapping 1 - Pedestrian.');
ylim([-2.5 2.5]);
subplot(3,2,6);
boxplot(Acceptance_pe.acc_2,'Labels',Acceptance_pe.Laan2.name);
ylabel('Score'); title('Acceptance score Mapping 2 - Pedestrian.');
ylim([-2.5 2.5]);

end

%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%% Tables %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
% Cronbach's values 
T_Cron = table(round([Acceptance_pa.cron_U0; Acceptance_pa.cron_U1; Acceptance_pa.cron_U2],2),... 
               round([Acceptance_pa.cron_S0; Acceptance_pa.cron_S1; Acceptance_pa.cron_S2],2),...
               round([Acceptance_pe.cron_U0; Acceptance_pe.cron_U1; Acceptance_pe.cron_U2],2),...
               round([Acceptance_pe.cron_S0; Acceptance_pe.cron_S1; Acceptance_pe.cron_S2],2),...
                'VariableNames',{'Passenger - Usefulness' 'Passenger - Satisfying' 'Pedestrian - Usefulness' 'Pedestrian - Satisfying'},...
                'RowNames',{'Baseline' 'Mapping 1' 'Mapping 2'});

% Acceptance score passenger
T_acPa = table(round(Acceptance_pa.Laan0.meanAcc,3)',round(Acceptance_pa.Laan1.meanAcc,3)',round(Acceptance_pa.Laan2.meanAcc,3)',...
               round(Acceptance_pe.Laan0.meanAcc,3)',round(Acceptance_pe.Laan1.meanAcc,3)',round(Acceptance_pe.Laan2.meanAcc,3)',...
               'VariableNames',{'Passenger - Baseline' 'Passenger - Mapping 1' 'Passenger - Mapping 2' 'Pedestrian - Baseline' 'Pedestrian - Mapping 1' 'Pedestrian - Mapping 2'},...
               'RowNames',Acceptance_pa.Laan0.name);
           
if(ShowTables==true)
	disp(T_Cron);
	disp('According to Van Der Laan, a Cronbachs alpha above 0.65 is needed to deem the reliability of the subscale scores sufficiently high.');
    disp(T_acPa);
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
function [Data_baseline, Data_map1, Data_map2] = matchWithMappingAcceptance(dataMap, data)
Idx0 = contains(dataMap,'Baseline');
Idx1 = contains(dataMap,'Mapping 1 (stop when looking)');
Idx2 = contains(dataMap,'Mapping 2 (stop when NOT looking');
Data_baseline = data(Idx0,:);
Data_map1     = data(Idx1,:);
Data_map2     = data(Idx2,:);
end
function acpt = calcAcceptance(dataMap, data)
[acpt.acc_0, acpt.acc_1, acpt.acc_2] = matchWithMappingAcceptance(dataMap,data);
% VanDerLaan scales from +2 to -2
acpt.acc_0 = acpt.acc_0-3; acpt.acc_1 = acpt.acc_1-3; acpt.acc_2 = acpt.acc_2-3;
% Items 3,6,and 8 scale from -2 to +2
acpt.acc_0(:,[1,2,4,5,7,9]) = acpt.acc_0(:,[1,2,4,5,7,9])*-1;
acpt.acc_1(:,[1,2,4,5,7,9]) = acpt.acc_1(:,[1,2,4,5,7,9])*-1;
acpt.acc_2(:,[1,2,4,5,7,9]) = acpt.acc_2(:,[1,2,4,5,7,9])*-1;

%% Reliability analyses
[acpt.Laan0, acpt.usefulness_score0, acpt.satisfying_score0, dataU0, dataS0] = calcVanDerLaanScore(acpt.acc_0);
[acpt.Laan1, acpt.usefulness_score1, acpt.satisfying_score1, dataU1, dataS1] = calcVanDerLaanScore(acpt.acc_1);
[acpt.Laan2, acpt.usefulness_score2, acpt.satisfying_score2, dataU2, dataS2] = calcVanDerLaanScore(acpt.acc_2);

% Cronbach's alpha
acpt.cron_U0 = calcCronbach(dataU0);
acpt.cron_U1 = calcCronbach(dataU1);
acpt.cron_U2 = calcCronbach(dataU2);
acpt.cron_S0 = calcCronbach(dataS0);
acpt.cron_S1 = calcCronbach(dataS1);
acpt.cron_S2 = calcCronbach(dataS2);
end
function [Laan, usefulness_score, satisfying_score, datausefulness, datasatisfying] = calcVanDerLaanScore(data)
% Usefulness; use item: 1,3,5,7,and 9
datausefulness = data(:,[1,3,5,7,9]);
datasatisfying = data(:,[2,4,6,8]);
usefulness_score = mean(datausefulness,'all');
satisfying_score = mean(datasatisfying,'all');
Laan.name       = {'useful','pleasant','good','nice','effective','likeable','assisting','desirable','raising alertness'};
Laan.meanAcc    = mean(data);
Laan.meanmean   = mean(Laan.meanAcc);
Laan.useful     = Laan.meanAcc(1);
Laan.pleasant   = Laan.meanAcc(2);
Laan.good       = Laan.meanAcc(3);
Laan.nice       = Laan.meanAcc(4);
Laan.effective  = Laan.meanAcc(5);
Laan.likeable   = Laan.meanAcc(6);
Laan.assisting  = Laan.meanAcc(7);
Laan.desirable  = Laan.meanAcc(8);
Laan.raisingAlertness = Laan.meanAcc(9);
end
function alpha = calcCronbach(data)
if nargin<1 || isempty(data)==1
   error('You shoud provide a data set.');
else
   % X must be a 2 dimensional matrix
   if ~ismatrix(data)
      error('Invalid data set.');
   end
end
% Calculate the number of items
k=size(data,2);
% Calculate the variance of the items' sum
VarTotal=var(sum(data'));
% Calculate the item variance
SumVarX=sum(var(data));
% Calculate the Cronbach's alpha
alpha=k/(k-1)*(VarTotal-SumVarX)/VarTotal;
end