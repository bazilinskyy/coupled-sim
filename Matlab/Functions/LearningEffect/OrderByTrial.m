%% Order by trial
% Author: Johnson Mok
% Last Updated: 24-02-2021

function out = OrderByTrial(data, ED, PARTI)
%% fieldnames
fld_ED = fieldnames(data);
ed = find(strcmp(fld_ED,ED));
fld_parti = fieldnames(data.(fld_ED{ed}).HostFixedTimeLog);
parti = find(strcmp(fld_parti,PARTI));
fld_trial = fieldnames(data.(fld_ED{ed}).HostFixedTimeLog.(fld_parti{parti}));

%% Set by trial order
order = findOrder(fld_trial);
dat = data.(fld_ED{ed}).HostFixedTimeLog.(fld_parti{parti});

%% Phase idx
idx = cell(1,length(order));
for i=1:length(order)
    idx{i} = getPhaseIdx(dat.(fld_trial{order(i)}));
end
if length(idx{1}) == 3
    st = 3;
else
    st = 4;
end
    
%% Output
if length(order)>=1
    out.trial_1 = dat.(fld_trial{order(1)}).pe.gapAcceptance(idx{1}(1,1):idx{1}(2,st));
end
if length(order)>=2
    out.trial_2 = dat.(fld_trial{order(2)}).pe.gapAcceptance(idx{2}(1,1):idx{2}(2,st));
end
if length(order)>=3
    out.trial_3 = dat.(fld_trial{order(3)}).pe.gapAcceptance(idx{3}(1,1):idx{3}(2,st));
end
if length(order)==4
    out.trial_4 = dat.(fld_trial{order(4)}).pe.gapAcceptance(idx{4}(1,1):idx{4}(2,st));
end
end

%% Helper function
function order = findOrder(fld_trial)
trial_split = split(fld_trial,'_');
numb = trial_split(:,2);
trials = zeros(length(numb),1);
for i=1:length(numb)
    trials(i) = str2double(numb{i});
end
if length(numb)==1
    order = 1;
elseif length(numb)==2
    [~, idx_1] = min(trials);
    [~, idx_2] = max(trials); 
    order = [idx_1; idx_2];
elseif length(numb)==3
    [~, idx_1] = min(trials);
    [~, idx_3] = max(trials);
    trials([idx_1, idx_3]) = NaN;
    idx_2 = find(~isnan(trials));
    order = [idx_1; idx_2; idx_3];
elseif length(numb)==4
    [~, idx_1] = min(trials);
    [~, idx_4] = max(trials);
    trials([idx_1, idx_4]) = NaN;
    [~, idx_2] = min(trials);
    [~, idx_3] = max(trials);
    order = [idx_1; idx_2; idx_3; idx_4];
end
end

function idx = getPhaseIdx(data)
pos = data.pa.pos.z;
pos_rel = pos - 17.19;
dx = abs(gradient(pos_rel));

ph1 = [1; find(pos_rel<25, 1,'first')];                             % Phase 1, start till start trigger range
ph2 = [find(pos_rel<25, 1,'first'); find(pos_rel<14.4, 1,'first')]; % Phase 2, trigger range
ph3 = [find(pos_rel<14.4, 1,'first'); find(dx<0.01, 1,'first')];    % Phase 3, end trigger range till standstill
ph4 = [];
ph5 = [];
if(find(dx<0.001, 1,'last')<find(pos_rel<-4.69, 1,'first'))
    ph4 = [find(dx<0.001, 1,'first'); find(dx<0.001, 1,'last')];          % Phase 4, AV standstill
    ph5 = [find(dx<0.001, 1,'last'); find(pos_rel<-4.69, 1,'first')];    % Phase 5, AV start driving again till the AV is completely past the zebra crossing
end
if(find(dx<0.001, 1,'last')>find(pos_rel<-4.69, 1,'first'))
    ph4 = [find(dx<0.01, 1,'first'); find(dx<0.0001, 1,'last')];          
    ph5 = [find(dx<0.0001, 1,'last'); find(pos_rel<-4.69, 1,'first')];
end
if(find(dx<0.0001, 1,'last')>find(pos_rel<-4.69, 1,'first'))
    ph4 = [find(dx<0.01, 1,'first'); find(dx<0.00001, 1,'last')];          
    ph5 = [find(dx<0.00001, 1,'last'); find(pos_rel<-4.69, 1,'first')];
end
if(~isempty(ph4))% Case: Yielding
    idx = [ph1, ph2, ph3, ph4, ph5];
elseif(isempty(ph4)) % Case: Non yielding
    phNY = [find(pos_rel<14.4, 1,'first'); find(pos_rel<-4.69, 1,'first')]; % Phase NY, end trigger range till the AV is completely past the zebra crossing
    idx = [ph1, ph2, phNY];
end
% Testing
% figure
% plot(pos_rel)
% % xline(ph1(2))
% % xline(ph2(2))
% xline(ph3(2))
% xline(ph4(2))
% % xline(ph5(2))
end







