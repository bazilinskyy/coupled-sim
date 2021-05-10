%% Analyze gaze time
% This script takes in the gazing times from both the pedestrian and
% pedestrian and analyses the data.
% Author: Johnson Mok
% Last Updated: 04-02-2021

% Input
%

% Output
%

function out = analyzeGazeTime(timesgroup, pa_dis, pe_dis)
%% Eye-contact
out.EC = getOrganisedData(timesgroup,'eyeContact');

%% Passenger gaze time
out.pa_full_watch = getOrganisedData(timesgroup,'pa','full','watch');

% Passenger gazing at pedestrian during phases 
out.wP2 = getOrganisedData(timesgroup, 'pa', 'time2', 'watch');% Phase 2: Tracking till AV stop
out.wP3 = getOrganisedData(timesgroup, 'pa', 'time3', 'watch');% Phase 3: During yield
out.wP4 = getOrganisedData(timesgroup, 'pa', 'time4', 'watch');% Phase 4: After reset

% Passenger invalid tracking during phases
out.invP2 = getOrganisedData(timesgroup, 'pa', 'time2', 'invalid');
out.invP3 = getOrganisedData(timesgroup, 'pa', 'time3', 'invalid');
out.invP4 = getOrganisedData(timesgroup, 'pa', 'time4', 'invalid');

% Passenger not watching pedestrian during phases
out.nwP2 = getOrganisedData(timesgroup, 'pa', 'time2', 'nowatch');
out.nwP3 = getOrganisedData(timesgroup, 'pa', 'time3', 'nowatch');
out.nwP4 = getOrganisedData(timesgroup, 'pa', 'time4', 'nowatch');

%% Calculate mean
out.mean_wP2 = meanStruct(out.wP2);
out.mean_wP3 = meanStruct(out.wP3);
out.mean_wP4 = meanStruct(out.wP4);

out.mean_invP2 = meanStruct(out.invP2);
out.mean_invP3 = meanStruct(out.invP3);
out.mean_invP4 = meanStruct(out.invP4);

out.mean_nwP2 = meanStruct(out.nwP2);
out.mean_nwP3 = meanStruct(out.nwP3);
out.mean_nwP4 = meanStruct(out.nwP4);

%% Number of people watching at distance z. Divided by phases
out.pa_dis_org = getOrganisedData(pa_dis,'distance');
out.pe_dis_org = getOrganisedData(pe_dis,'distance');

[out.pa_val.ND_Y, out.pa_name.ND_Y] = sumDis(out.pa_dis_org.ND_Y);
[out.pa_val.ND_NY, out.pa_name.ND_NY] = sumDis(out.pa_dis_org.ND_NY);
[out.pa_val.D_Y, out.pa_name.D_Y] = sumDis(out.pa_dis_org.D_Y);
[out.pa_val.D_NY, out.pa_name.D_NY] = sumDis(out.pa_dis_org.D_NY);

[out.pe_val.ND_Y, out.pe_name.ND_Y] = sumDis(out.pe_dis_org.ND_Y);
[out.pe_val.ND_NY, out.pe_name.ND_NY] = sumDis(out.pe_dis_org.ND_NY);
[out.pe_val.D_Y, out.pe_name.D_Y] = sumDis(out.pe_dis_org.D_Y);
[out.pe_val.D_NY, out.pe_name.D_NY] = sumDis(out.pe_dis_org.D_NY);

end

%% Helper functions
function out = getOrganisedData(data,lvl1,varargin)
LVL1 = fieldnames(data.Data_ED_0.HostFixedTimeLog);         % lvl1 = 'eyeContact', 'pa', 'pe', or 'distance'
if(~strcmp(lvl1,'distance'))
    LVL2 = fieldnames(data.Data_ED_0.HostFixedTimeLog.pa);      % lvl2 = 'full', 'time1', 'time2', 'time3' or 'time4'.
    LVL3 = fieldnames(data.Data_ED_0.HostFixedTimeLog.pa.full); % lvl3 = 'nowatch', 'invalid', 'total', or 'watch'
    idx1 = find(strcmp(LVL1,lvl1));
end
if (nargin>2)
    idx2 = find(strcmp(LVL2,varargin{1}));
    idx3 = find(strcmp(LVL3,varargin{2}));
end
if (nargin==2 && strcmp(lvl1,'eyeContact'))
% ND_Y: ED 0, 4, 8
ND_Y.map0 = data.Data_ED_0.HostFixedTimeLog.(LVL1{idx1})';
ND_Y.map1 = data.Data_ED_4.HostFixedTimeLog.(LVL1{idx1})';
ND_Y.map2 = data.Data_ED_8.HostFixedTimeLog.(LVL1{idx1})';
% ND_NY: ED 1, 5, 9
ND_NY.map0 = data.Data_ED_1.HostFixedTimeLog.(LVL1{idx1})';
ND_NY.map1 = data.Data_ED_5.HostFixedTimeLog.(LVL1{idx1})';
ND_NY.map2 = data.Data_ED_9.HostFixedTimeLog.(LVL1{idx1})';
% D_Y: ED 2, 6, 10
D_Y.map0 = data.Data_ED_2.HostFixedTimeLog.(LVL1{idx1})';
D_Y.map1 = data.Data_ED_6.HostFixedTimeLog.(LVL1{idx1})';
D_Y.map2 = data.Data_ED_10.HostFixedTimeLog.(LVL1{idx1})';
% D_NY: ED 1, 5, 9
D_NY.map0 = data.Data_ED_3.HostFixedTimeLog.(LVL1{idx1})';
D_NY.map1 = data.Data_ED_7.HostFixedTimeLog.(LVL1{idx1})';
D_NY.map2 = data.Data_ED_11.HostFixedTimeLog.(LVL1{idx1})';
elseif (nargin>2)
    ND_Y.map0 = data.Data_ED_0.HostFixedTimeLog.(LVL1{idx1}).(LVL2{idx2}).(LVL3{idx3})';
    ND_Y.map1 = data.Data_ED_4.HostFixedTimeLog.(LVL1{idx1}).(LVL2{idx2}).(LVL3{idx3})';
    ND_Y.map2 = data.Data_ED_8.HostFixedTimeLog.(LVL1{idx1}).(LVL2{idx2}).(LVL3{idx3})';
    % ND_NY: ED 1, 5, 9
    ND_NY.map0 = data.Data_ED_1.HostFixedTimeLog.(LVL1{idx1}).(LVL2{idx2}).(LVL3{idx3})';
    ND_NY.map1 = data.Data_ED_5.HostFixedTimeLog.(LVL1{idx1}).(LVL2{idx2}).(LVL3{idx3})';
    ND_NY.map2 = data.Data_ED_9.HostFixedTimeLog.(LVL1{idx1}).(LVL2{idx2}).(LVL3{idx3})';
    % D_Y: ED 2, 6, 10
    D_Y.map0 = data.Data_ED_2.HostFixedTimeLog.(LVL1{idx1}).(LVL2{idx2}).(LVL3{idx3})';
    D_Y.map1 = data.Data_ED_6.HostFixedTimeLog.(LVL1{idx1}).(LVL2{idx2}).(LVL3{idx3})';
    D_Y.map2 = data.Data_ED_10.HostFixedTimeLog.(LVL1{idx1}).(LVL2{idx2}).(LVL3{idx3})';
    % D_NY: ED 1, 5, 9
    D_NY.map0 = data.Data_ED_3.HostFixedTimeLog.(LVL1{idx1}).(LVL2{idx2}).(LVL3{idx3})';
    D_NY.map1 = data.Data_ED_7.HostFixedTimeLog.(LVL1{idx1}).(LVL2{idx2}).(LVL3{idx3})';
    D_NY.map2 = data.Data_ED_11.HostFixedTimeLog.(LVL1{idx1}).(LVL2{idx2}).(LVL3{idx3})';
elseif(nargin==2 && strcmp(lvl1,'distance'))
    % ND_Y: ED 0, 4, 8
    ND_Y.map0 = data.Data_ED_0.HostFixedTimeLog.(LVL1{:});
    ND_Y.map1 = data.Data_ED_4.HostFixedTimeLog.(LVL1{:});
    ND_Y.map2 = data.Data_ED_8.HostFixedTimeLog.(LVL1{:});
    % ND_NY: ED 1, 5, 9
    ND_NY.map0 = data.Data_ED_1.HostFixedTimeLog.(LVL1{:});
    ND_NY.map1 = data.Data_ED_5.HostFixedTimeLog.(LVL1{:});
    ND_NY.map2 = data.Data_ED_9.HostFixedTimeLog.(LVL1{:});
    % D_Y: ED 2, 6, 10
    D_Y.map0 = data.Data_ED_2.HostFixedTimeLog.(LVL1{:});
    D_Y.map1 = data.Data_ED_6.HostFixedTimeLog.(LVL1{:});
    D_Y.map2 = data.Data_ED_10.HostFixedTimeLog.(LVL1{:});
    % D_NY: ED 1, 5, 9
    D_NY.map0 = data.Data_ED_3.HostFixedTimeLog.(LVL1{:});
    D_NY.map1 = data.Data_ED_7.HostFixedTimeLog.(LVL1{:});
    D_NY.map2 = data.Data_ED_11.HostFixedTimeLog.(LVL1{:});
end
out.ND_Y = ND_Y;
out.ND_NY = ND_NY;
out.D_Y = D_Y;
out.D_NY = D_NY;
end

function out = meanStruct(in)
fld = fieldnames(in);
fld2 = fieldnames(in.(fld{1}));
out = [];
for i = 1:length(fld)
    for j = 1:length(fld2)
%         out(1,end+1:end+length(mean(in.(fld{i})))) = mean(in.(fld{i}));
        out(1,end+1) = mean(in.(fld{i}).(fld2{j}));
    end
end
end

function [val, dis] = sumDis(in) 
part = 10; % group by 1/part
fld = fieldnames(in);
for i = 1:length(fld)
    rnd = roundDis(in.(fld{i}),part);
    fill = fillUpArray(rnd);
    [val.(fld{i}), dis.(fld{i})] = countDis(fill,part);
    val.(fld{i}) = 100*(val.(fld{i})/length(in.(fld{i})));
end
val = [val.(fld{1}), val.(fld{2}), val.(fld{3})];
dis = [dis.(fld{1}), dis.(fld{2}), dis.(fld{3})];
end
function out = roundDis(in,part)
out = cell(size(in));
for j = 1:size(in,2)
	for k = 1:size(in,1)
%         out{k,j} = round(in{k,j},decimals);
        out{k,j} = fun_round(in{k,j},part);
	end
end
end
function out = fun_round(in,part)
up = in<round(in);
out = zeros(size(up));
for i =1:length(up)
    if(up(i) == true)
        out(i) = ceil(in(i)*part)/part;
    elseif(up(i) == false)
        out(i) = floor(in(i)*part)/part;
    end
end
end
function out = fillUpArray(in)
[max_size, ~] = max(cellfun('size', in, 1));
x = ceil((max(max_size))/50)*50;
tempout = zeros(x,1);
out = cell(size(in));
for j=1:size(in,2)                          % 3 columns
    for i=1:length(in)                      % 44 rows
        temp = in{i,j};                     % extract 1 cell array (e.g. 1340x1)
        tempout(1:length(temp)) = temp;     % Copy up to 1340
        for di = length(temp)+1:x
            tempout(di) = temp(end);
        end
        out{i,j} = tempout;
    end
end
end
function [val, dis] = countDis(in,part)
dis = (-1:(1/part):70)';
val = zeros(length(dis),size(in,2));
for col = 1:size(in,2)
    for row = 1:size(in,1)
        [GC, GR] = groupcounts(in{row,col});         % One cell array
        for i = 1:length(dis)
            index = find(GR==dis(i));                % index of value
            if(~isempty(index))
                val(i,col) = val(i,col) + 1; %GC(index); % add occurence of value
            end
        end
    end
end
end





