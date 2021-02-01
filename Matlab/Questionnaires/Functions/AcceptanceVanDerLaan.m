%% Acceptance Van Der Laan
% Author: Johnson Mok
% Last Updated: 27-01-2021

% This script analyses the acceptance questionnaire from Van Der Laan
% - Statistical significance using repeated measures ANOVA
% - Reliability analysis using Cronbach's alpha
% - Acceptance opinion change using the difference in scores between
% mappings.

% Input: 
% 1) dataMap: Matrix with mapping of trials.
% 2) data: Matrix with answers of Van Der Laan Questionnaire

% Output:
% acc(0/1/2): Van Der Laan quesionnaire answers ordened by mapping.
% Laan(0/1/2): Mean scores per question per mapping.
% all.(S/U).(0/1/2): Mean scores of the subscales satisfying and usefulness of all the participants.
% par.(S/U).(0/1/2): Mean scores of the subscales satisfying and usefulness per participant
% (S/U).(0/1/2): All scores related to the subscales per mapping.
% cron.(S/U).(0/1/2): Cronbach's alpha per mapping per subscale
% ANOVA: F value per subscale
% diff: Difference in all subscale per mapping.

function acpt = AcceptanceVanDerLaan(dataMap, data)
[acpt.acc_0, acpt.acc_1, acpt.acc_2] = matchWithMappingAcceptance(dataMap,data);
% VanDerLaan scales from +2 to -2
acpt.acc_0 = acpt.acc_0-3; acpt.acc_1 = acpt.acc_1-3; acpt.acc_2 = acpt.acc_2-3;
% Items 3,6,and 8 scale from -2 to +2
acpt.acc_0(:,[1,2,4,5,7,9]) = acpt.acc_0(:,[1,2,4,5,7,9])*-1;
acpt.acc_1(:,[1,2,4,5,7,9]) = acpt.acc_1(:,[1,2,4,5,7,9])*-1;
acpt.acc_2(:,[1,2,4,5,7,9]) = acpt.acc_2(:,[1,2,4,5,7,9])*-1;

%% Reliability analyses
[acpt.Laan0, acpt.all.U0, acpt.all.S0, acpt.all.stdU0, acpt.all.stdS0, acpt.U0, acpt.S0, acpt.par.U0, acpt.par.S0] = calcVanDerLaanScore(acpt.acc_0);
[acpt.Laan1, acpt.all.U1, acpt.all.S1, acpt.all.stdU1, acpt.all.stdS1, acpt.U1, acpt.S1, acpt.par.U1, acpt.par.S1] = calcVanDerLaanScore(acpt.acc_1);
[acpt.Laan2, acpt.all.U2, acpt.all.S2, acpt.all.stdU2, acpt.all.stdS2, acpt.U2, acpt.S2, acpt.par.U2, acpt.par.S2] = calcVanDerLaanScore(acpt.acc_2);

% Cronbach's alpha
acpt.cron.U0 = calcCronbach(acpt.U0);
acpt.cron.U1 = calcCronbach(acpt.U1);
acpt.cron.U2 = calcCronbach(acpt.U2);
acpt.cron.S0 = calcCronbach(acpt.S0);
acpt.cron.S1 = calcCronbach(acpt.S1);
acpt.cron.S2 = calcCronbach(acpt.S2);

%% Repeated measures ANOVA
statdata_U = [acpt.par.U0,acpt.par.U1,acpt.par.U2];
statdata_S = [acpt.par.S0,acpt.par.S1,acpt.par.S2];
[acpt.ANOVA.FU, acpt.ANOVA.TU] = rmANOVA(statdata_U);
[acpt.ANOVA.FS, acpt.ANOVA.TS] = rmANOVA(statdata_S);

%% Mauchly's test for sphericity
% The RepeatedMeasuresModel is incorrect. Checked with the ranova results.

% partnr = [1:length(statdata_U)]';
% t = table(partnr,statdata_U(:,1),statdata_U(:,2),statdata_U(:,3),'VariableNames',{'ParticipantNr','map0','map1','map2'});
% Meas = table([1 2 3]','VariableNames',{'Measurements'});
% rmU = fitrm(t,'map0-map2~ParticipantNr','WithinDesign',Meas);
% mauchly(rmU)
% ranova(rmU)

%% Difference in score (map-baseline)
acpt.diff.U1 = acpt.all.U1-acpt.all.U0;
acpt.diff.U2 = acpt.all.U2-acpt.all.U0;
acpt.diff.S1 = acpt.all.S1-acpt.all.S0;
acpt.diff.S2 = acpt.all.S2-acpt.all.S0;
end

%% Helper functions
function [Data_baseline, Data_map1, Data_map2] = matchWithMappingAcceptance(dataMap, data)
Idx0 = contains(dataMap,'Baseline');
Idx1 = contains(dataMap,'Mapping 1 (stop when looking)');
Idx2 = contains(dataMap,'Mapping 2 (stop when NOT looking');
Data_baseline = data(Idx0,:);
Data_map1     = data(Idx1,:);
Data_map2     = data(Idx2,:);
end

function [Laan,use_score,sat_score,use_score_std,sat_score_std,usefulness,satisfying,use_par,sat_par] = calcVanDerLaanScore(data)
usefulness = data(:,[1,3,5,7,9]);       % Usefulness; use item: 1,3,5,7,and 9
satisfying = data(:,[2,4,6,8]);         % Satisfying; use item: 2,4,6,and 8
% Subscale scores per participant
use_par = mean(usefulness,2);
sat_par = mean(satisfying,2);
% Subscale scores of all 
use_score = mean(usefulness,'all');
sat_score = mean(satisfying,'all');
% Subscale std of all
use_score_std = std(use_par);
sat_score_std = std(sat_par);
% Scores per scale
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

function [F,T] = rmANOVA(data)
% Table size
T.s = length(data);
T.a = size(data,2);
T.N = size(data,1)*size(data,2);
% DoF
T.df_b = T.a-1;
T.df_w = T.N-T.a;
T.df_s = T.s-1;
T.df_e = T.df_w-T.df_s;
T.df_t = T.N-1;
% Sum squared
T.SS_b = (sum(sum(data).^2)/T.s) - ((sum(data,'all')^2)/T.N);
T.SS_w = sum(data.^2,'all') - (sum(sum(data).^2)/T.s);
T.SS_s = (sum(sum(data,2).^2)/T.a) - ((sum(data,'all')^2)/T.N);
T.SS_e = T.SS_w - T.SS_s;
T.SS_t = T.SS_b + T.SS_w;
% mean squared
T.MS_b = T.SS_b/T.df_b;
T.MS_e = T.SS_e/T.df_e;
% result
F = T.MS_b/T.MS_e;
end