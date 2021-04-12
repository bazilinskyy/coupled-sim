%% Analyze gaze time V2
% This script takes in the gazing times from both the pedestrian and
% pedestrian and analyses the data.
% Author: Johnson Mok
% Last Updated: 17-02-2021

% Input
%

% Output
%

function out = analyzeGazeTimeV2(timesgroup, order)
out.orgTimes = getOrganisedData(timesgroup);
out.eyeContactPerPerson = meanPerPerson(out.orgTimes, order);
out.MeanStdEyeContact = meanPerCondition(out.eyeContactPerPerson);
out.mean = meanAllStruct(out.orgTimes);

% Number of people watching at distance z. Divided by phases
out.groupcounts = sumDisAll(out.orgTimes);

% Matrix for SPSS
getSPSSMatrix(out.groupcounts);

% Check whether the passenger followed the instructions
checkGaze = checkGazeLATY(out.orgTimes);
end

%% Helper functions
function out = getOrganisedData(data)
fld_person = fieldnames(data.Data_ED_0.HostFixedTimeLog); % lvl1 = 'eyeContact', 'pa', 'pe'
for p=1:length(fld_person)
    % ND_Y: ED 0, 4, 8
    ND_Y.map0.(fld_person{p}) = data.Data_ED_0.HostFixedTimeLog.(fld_person{p});
    ND_Y.map1.(fld_person{p}) = data.Data_ED_4.HostFixedTimeLog.(fld_person{p});
    ND_Y.map2.(fld_person{p}) = data.Data_ED_8.HostFixedTimeLog.(fld_person{p});
    % ND_NY: ED 1, 5, 9
    ND_NY.map0.(fld_person{p}) = data.Data_ED_1.HostFixedTimeLog.(fld_person{p});
    ND_NY.map1.(fld_person{p}) = data.Data_ED_5.HostFixedTimeLog.(fld_person{p});
    ND_NY.map2.(fld_person{p}) = data.Data_ED_9.HostFixedTimeLog.(fld_person{p});
    % D_Y: ED 2, 6, 10
    D_Y.map0.(fld_person{p}) = data.Data_ED_2.HostFixedTimeLog.(fld_person{p});
    D_Y.map1.(fld_person{p}) = data.Data_ED_6.HostFixedTimeLog.(fld_person{p});
    D_Y.map2.(fld_person{p}) = data.Data_ED_10.HostFixedTimeLog.(fld_person{p});
    % D_NY: ED 1, 5, 9
    D_NY.map0.(fld_person{p}) = data.Data_ED_3.HostFixedTimeLog.(fld_person{p});
    D_NY.map1.(fld_person{p}) = data.Data_ED_7.HostFixedTimeLog.(fld_person{p});
    D_NY.map2.(fld_person{p}) = data.Data_ED_11.HostFixedTimeLog.(fld_person{p});
end

out.ND_Y = ND_Y;
out.ND_NY = ND_NY;
out.D_Y = D_Y;
out.D_NY = D_NY;
end

function out = meanStruct(data, con, mapping, person)
fld_con = fieldnames(data);
c = find(strcmp(fld_con,con));
fld_map = fieldnames(data.(fld_con{c}));
m = find(strcmp(fld_map,mapping));
fld_person = fieldnames(data.(fld_con{c}).(fld_map{m}));
p = find(strcmp(fld_person,person));

out = [];
if(~strcmp(person,'eyeContact'))
    fld_phase = fieldnames(data.(fld_con{c}).(fld_map{m}).(fld_person{p}));
    for q=1:length(fld_phase)
        fld_type = fieldnames(data.(fld_con{c}).(fld_map{m}).(fld_person{p}).(fld_phase{q}));
        if(~isfield(out,(fld_phase{q})))
            out.(fld_phase{q}) = [];
        end
        for t=1:length(fld_type)
            if(~isfield(out.(fld_phase{q}),(fld_type{t})))
                out.(fld_phase{q}).(fld_type{t}) = [];
            end
            if(~strcmp(fld_type{t},'distance'))
                out.(fld_phase{q}).(fld_type{t})(1,end+1) = mean(data.(fld_con{c}).(fld_map{m}).(fld_person{p}).(fld_phase{q}).(fld_type{t}));
%             else
%                 out.(fld_phase{q}).(fld_type{t})(1,end+1) = mean(data.(fld_con{c}).(fld_map{m}).(fld_person{p}).(fld_phase{q}).(fld_type{t}));
            end
        end
    end
else
    meandata = mean(data.(fld_con{c}).(fld_map{m}).(fld_person{p}));
    stddata = std(data.(fld_con{c}).(fld_map{m}).(fld_person{p}));
    out = [meandata, stddata];
end

end
function out = meanAllStruct(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        fld_person = fieldnames(data.(fld_con{c}).(fld_map{m}));
%         for p=1:length(fld_person)
        out.(fld_con{c}).(fld_map{m}) = meanStruct(data, fld_con{c}, fld_map{m}, fld_person{1}); %out.(fld_con{c}).(fld_map{m}).(fld_person{p}) = meanStruct(data, fld_con{c}, fld_map{m}, fld_person{p})
%         end
    end
end
end

function [freq, val] = sumDis(in) 
val_ = cell(size(in));
for i=1:length(in)
    data_round = round(in{i},1);
    val_{i} = unique(data_round);
end
data_cell = reshape(val_,[length(val_),1]);
data_mat = cell2mat(data_cell);
[freq,val] = groupcounts(data_mat);
freq = 100*freq/length(in);
freq = smooth(freq);
% data_cell = reshape(in,[length(in),1]);
% data_mat = cell2mat(data_cell);
% data_round = round(data_mat,2);
% [freq,val] = groupcounts(data_round);
end
function out = sumDisAll(in)
fld_con = fieldnames(in);
for c=1:length(fld_con)
    fld_map = fieldnames(in.(fld_con{c}));
    for m=1:length(fld_map)
        fld_person = fieldnames(in.(fld_con{c}).(fld_map{m}));
        for per=1:length(fld_person)
            if(~strcmp(fld_person{per},'eyeContact'))
                fld_phase = fieldnames(in.(fld_con{c}).(fld_map{m}).(fld_person{per}));
                for p=1:length(fld_phase)
                    [out.(fld_con{c}).(fld_map{m}).(fld_person{per}).(fld_phase{p}).freq, ...
                        out.(fld_con{c}).(fld_map{m}).(fld_person{per}).(fld_phase{p}).val] = ...
                        sumDis(in.(fld_con{c}).(fld_map{m}).(fld_person{per}).(fld_phase{p}).distance);
                end
            end
        end
    end
end
end

function [distr,yield] = getCon(con)
if(strcmp(con,'ND_Y'))
    distr = 0;
    yield = 1;
elseif(strcmp(con,'ND_NY'))
    distr = 0;
    yield = 0;
elseif(strcmp(con,'D_Y'))
    distr = 1;
    yield = 1;
elseif(strcmp(con,'D_NY'))
    distr = 1;
    yield = 0;
end
end
function map = getMap(mapping)
if(strcmp(mapping,'map0'))
    map = 0;
elseif(strcmp(mapping,'map1'))
    map = 1;
elseif(strcmp(mapping,'map2'))
    map = 2;
end
end
function phase = getPhase(p)
if(strcmp(p,'phase1'))
    phase = 1;
elseif(strcmp(p,'phase2'))
    phase = 2;
elseif(strcmp(p,'phase3'))
    phase = 3;
elseif(strcmp(p,'phase4'))
    phase = 4;
elseif(strcmp(p,'phase5'))
    phase = 5;
elseif(strcmp(p, 'full'))
    phase = 10;
end
end

function getSPSSMatrix(data)
% data.D_NY.map0.pa.phase1.freq
M1 = [];
M2 = [];
M3 = [];
M4 = [];
M5 = [];
M6 = [];
fld_per = fieldnames(data.ND_Y.map0);
fld_con = fieldnames(data);
for per=1:length(fld_per)
    for c=1:length(fld_con)
        [distr,yield] = getCon(fld_con{c});
        fld_map = fieldnames(data.(fld_con{c}));
        for m=1:length(fld_map)
            map = getMap(fld_map{m});
            fld_phase = fieldnames(data.(fld_con{c}).(fld_map{m}).(fld_per{per}));
            for p=1:length(fld_phase)
                phase = getPhase(fld_phase{p});
                freq = data.(fld_con{c}).(fld_map{m}).(fld_per{per}).(fld_phase{p}).freq;
                val = data.(fld_con{c}).(fld_map{m}).(fld_per{per}).(fld_phase{p}).val;
                % Create matrix
                M1 = [M1; yield*ones(size(freq))];
                M2 = [M2; distr*ones(size(freq))];
                M3 = [M3; map*ones(size(freq))];
                M4 = [M4; phase*ones(size(freq))];
                M5 = [M5; val];
                M6 = [M6; freq];
            end
        end
    end
    M = [M1, M2, M3, M4, M5, M6];
    T = array2table(M,'VariableNames',{'yield','distraction','mapping','phase','val','freq'});
    savename = join(['GazeTime_',fld_per{per},'.csv']);
    writetable(T, savename); 
end
end

function out = checkGazeLATY(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_phase = fieldnames(data.(fld_con{c}).map2.pa);
    if length(fld_phase)>1
        for i=1:length(data.(fld_con{c}).map2.pa.full.distance)
            dis = data.(fld_con{c}).map2.pa.full.distance{i};
            gazeDist.(fld_con{c}){i} = dis(find(dis>0));
        end
    else
        for i=1:length(data.(fld_con{c}).map2.pa.phase1.distance)
            dis = data.(fld_con{c}).map2.pa.phase1.distance{i};
            gazeDist.(fld_con{c}){i} = dis(find(dis>0));
        end
    end
end

for j=1:length(gazeDist.ND_Y)
    out.ND_Y{j} = gazeDist.ND_Y{j}(find( (gazeDist.ND_Y{j}> 14.4) & (gazeDist.ND_Y{j}< 25) ));
end
for j=1:length(gazeDist.D_Y)
    out.D_Y{j} = gazeDist.D_Y{j}(find( (gazeDist.D_Y{j}> 14.4) & (gazeDist.D_Y{j}< 25) ));
end
for j=1:length(gazeDist.ND_NY)
    out.ND_NY{j} = gazeDist.ND_NY{j}(find( (gazeDist.ND_NY{j}> 0) & (gazeDist.ND_NY{j}< 25) ));
end
for j=1:length(gazeDist.D_NY)
    out.D_NY{j} = gazeDist.D_NY{j}(find( (gazeDist.D_NY{j}> 0) & (gazeDist.D_NY{j}< 25) ));
end



end

function out = meanPerPerson(data, order)
fld_con = fieldnames(order);
for c = 1:length(fld_con)
    fld_map = fieldnames(order.(fld_con{c}));
    for m = 1:length(fld_map)
        A = data.(fld_con{c}).(fld_map{m}).eyeContact;
        B = order.(fld_con{c}).(fld_map{m}).Pnr;
        for i = min(order.(fld_con{c}).(fld_map{m}).Pnr):max(order.(fld_con{c}).(fld_map{m}).Pnr)
            out.(fld_con{c})(i,m) = mean(A(find(B==i)));   
        end
    end
end
end
function out = meanPerCondition(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    out.(fld_con{c}).mean = mean(data.(fld_con{c}),1);
    out.(fld_con{c}).std = std(data.(fld_con{c}),1);
end
end
