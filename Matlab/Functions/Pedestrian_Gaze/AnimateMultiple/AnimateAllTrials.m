%% AnimateAllTrials
% Animate all trials for a qualitative check of laser intersection
% Hierarchy: AnimateAllTrials -> InputTrialAll -> AnimateTrialAll
% Author: Johnson Mok

function AnimateAllTrials(data,intersect)
fld_ED = fieldnames(data);
for ed=1:length(fld_ED)
    fld_part = fieldnames(data.(fld_ED{ed}).HostFixedTimeLog);
    part_chosen = ceil(rand(1,2)*(length(fld_part)-1)); % randomly select 2 participants
    for p=part_chosen %1:length(fld_part)
        fld_trial = fieldnames(data.(fld_ED{ed}).HostFixedTimeLog.(fld_part{p}));
        trial_chosen = ceil(rand(1,1)*(length(fld_trial)-1)); % randomly select 1 trial
        for tr= trial_chosen %1:length(fld_trial)
            input = data.(fld_ED{ed}).HostFixedTimeLog.(fld_part{p}).(fld_trial{tr});
            int2D = intersect.(fld_ED{ed}).HostFixedTimeLog.(fld_part{p}).(fld_trial{tr});
            name = [fld_ED{ed},'_',(fld_part{p}),'_',(fld_trial{tr}),'.avi'];
            title = getConTitle(fld_ED{ed});
            titleLaser = strrep([fld_part{p},'_',fld_trial{tr}],'_','-');
            InputTrialAll(input, int2D, name, title, titleLaser);
        end
    end
end
end

%% helper function
function out = getConTitle(con)
strSplit = split(con,'_');
EDnr = str2double(cell2mat(strSplit(3)));

ND_Y = [0, 4, 8];
ND_NY = [1, 5, 9];
D_Y = [2, 6, 10];
D_NY = [3, 7, 11];

m0 = 0:3;
m1 = 4:7;
m2 = 8:11;

if(ismember(EDnr,ND_Y))
    c = 'ND-Y';
elseif(ismember(EDnr,ND_NY))
    c = 'ND-NY';
elseif(ismember(EDnr,D_Y))
    c = 'D-Y';
elseif(ismember(EDnr,D_NY))
    c = 'D-NY';
end

if(ismember(EDnr,m0))
    m = 'baseline';
elseif(ismember(EDnr,m1))
    m = 'GTY';
elseif(ismember(EDnr,m2))
    m = 'LATY';
end

out = [m,'-',c];
end

