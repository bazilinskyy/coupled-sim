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
EDnrtemp = split(ED,'_');
EDnr = str2double(EDnrtemp{3});
idx = cell(1,length(order));
for i=1:length(order)
    idx{i} = getPhaseIdx(dat.(fld_trial{order(i)}),EDnr);
end
if length(idx{1}) == 3
    st = 3;
else
    st = 2;
end
    
%% Output
if length(order)>=1
    out.trial_1 = dat.(fld_trial{order(1)}).pe.gapAcceptance(idx{1}(1,2):idx{1}(2,end));
end
if length(order)>=2
    out.trial_2 = dat.(fld_trial{order(2)}).pe.gapAcceptance(idx{2}(1,2):idx{2}(2,end));
end
if length(order)>=3
    out.trial_3 = dat.(fld_trial{order(3)}).pe.gapAcceptance(idx{3}(1,2):idx{3}(2,end));
end
if length(order)==4
    out.trial_4 = dat.(fld_trial{order(4)}).pe.gapAcceptance(idx{4}(1,2):idx{4}(2,end));
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

function idx = getPhaseIdx(data, ED)
pos = data.pa.pos.z;
pos_rel = pos - 17.19;
dx = abs(gradient(pos_rel));
Y = 0:2:10;
NY = 1:2:11;
if sum(ismember(Y,ED))
    yield = 1;
elseif sum(ismember(NY,ED))
    yield = 0;
end

if yield == 1
    % indices yield
    idx_start = 1;
    idx_decel_start = find(pos<42.19,1,'first');
    idx_standstill_start = find(dx<0.01, 1,'first');
    idx_standstill_end = idx_standstill_start + round(2.6/0.0167);

    % Set up phases
    ph1 = [idx_start; idx_decel_start];
    ph2 = [idx_decel_start; idx_standstill_start];
    ph3 = [idx_standstill_start; idx_standstill_end];

    % Set up idx array
    idx = [ph1, ph2, ph3];
elseif yield == 0
    % indices nonyield
    idx_start = 1;
    idx_25 = find(pos_rel< 25, 1, 'first');
    idx_past_zebra = find(pos_rel< - 4.69, 1, 'first');

    % Set up phases
    ph1 = [idx_start; idx_25];
    ph2 = [idx_25; idx_past_zebra];

    % Set up idx array
    idx = [ph1, ph2];
end
end







