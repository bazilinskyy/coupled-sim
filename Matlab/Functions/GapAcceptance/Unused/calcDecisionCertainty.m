%% Calculate decision certainty
% This script ...
% Author: Johnson Mok
% Last Updated: 01-03-2021


function meanDC_per_participant = calcDecisionCertainty(data, phases)
data_phase = getPhase(data, phases);
data_org = getOrganizedDY(data_phase);
meanDC_per_participant = calcAllDC(data_org);
T1 = createSPSSMatrix(meanDC_per_participant, 'ND_Y', 'DecisionCertainty_SPSS_ND_Y.csv');
T2 = createSPSSMatrix(meanDC_per_participant, 'ND_NY', 'DecisionCertainty_SPSS_ND_NY.csv');
T3 = createSPSSMatrix(meanDC_per_participant, 'D_Y', 'DecisionCertainty_SPSS_D_Y.csv');
T4 = createSPSSMatrix(meanDC_per_participant, 'D_NY', 'DecisionCertainty_SPSS_D_NY.csv');

end
%% Helper functions
function out = getPhase(data, phases)
fld_ED = fieldnames(data);
for ed = 1:length(fld_ED)
    fld_par = fieldnames(data.(fld_ED{ed}).HostFixedTimeLog);
    for par = 1:length(fld_par)
        fld_trial = fieldnames(data.(fld_ED{ed}).HostFixedTimeLog.(fld_par{par}));
        for trial = 1:length(fld_trial)
        	gap = data.(fld_ED{ed}).HostFixedTimeLog.(fld_par{par}).(fld_trial{trial}).pe.gapAcceptance; 
            id = phases.(fld_ED{ed}).HostFixedTimeLog.(fld_par{par}).(fld_trial{trial}).idx;
            upTo = 4;
            if upTo>length(id)
                upTo = 3;
            end
            out.(fld_ED{ed}).(fld_par{par}).(fld_trial{trial}) = gap(id(1,1):id(2,upTo));
        end
    end
end
end

function out = getOrganizedDY(data)
    % ND_Y: ED 0, 4, 8
    ND_Y.map0 = data.Data_ED_0;
    ND_Y.map1 = data.Data_ED_4;
    ND_Y.map2 = data.Data_ED_8;
    % ND_NY: ED 1, 5, 9
    ND_NY.map0 = data.Data_ED_1;
    ND_NY.map1 = data.Data_ED_5;
    ND_NY.map2 = data.Data_ED_9;
    % D_Y: ED 2, 6, 10
    D_Y.map0 = data.Data_ED_2;
    D_Y.map1 = data.Data_ED_6;
    D_Y.map2 = data.Data_ED_10;
    % D_NY: ED 1, 5, 9
    D_NY.map0 = data.Data_ED_3;
    D_NY.map1 = data.Data_ED_7;
    D_NY.map2 = data.Data_ED_11;

out.ND_Y = ND_Y;
out.ND_NY = ND_NY;
out.D_Y = D_Y;
out.D_NY = D_NY;
end      

function out = calcDC(data, con, map)
fld_con = fieldnames(data);
c = find(strcmp(fld_con,con));
fld_map = fieldnames(data.(fld_con{c}));
m = find(strcmp(fld_map,map));

fld_par = fieldnames(data.(fld_con{c}).(fld_map{m}));
for par = 1:length(fld_par)
    fld_trial = fieldnames(data.(fld_con{c}).(fld_map{m}).(fld_par{par}));
    temp = zeros(1,4);
    for tr = 1:length(fld_trial)
        temp(tr) = sum(abs(diff(data.(fld_con{c}).(fld_map{m}).(fld_par{par}).(fld_trial{tr}))));
    end
    out.(fld_par{par}) = mean(temp);
end
end
function out = calcAllDC(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        out.(fld_con{c}).(fld_map{m}) = calcDC(data, fld_con{c}, fld_map{m});
    end
end
end

function out = createSPSSMatrix(data, con, tablename)
% M_yield = [];
% M_dis = [];
M_map = [];
M_par = [];
M_dc = [];
fld_con = fieldnames(data);
for c=find(strcmp(fld_con,con)) %1:length(fld_con)
%     if strcmp(fld_con{c},'ND_Y')
%         distraction = 0;
%         yield = 1;
%     elseif strcmp(fld_con{c},'ND_NY')
%         distraction = 0;
%         yield = 0;
%     elseif strcmp(fld_con{c},'D_Y')
%         distraction = 1;
%         yield = 1;
%     elseif strcmp(fld_con{c},'D_NY')
%         distraction = 1;
%         yield = 0;        
%     end
    
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        if strcmp(fld_map{m},'map0')
            mapping = 0;
        elseif strcmp(fld_map{m},'map1')  
            mapping = 1;
        elseif strcmp(fld_map{m},'map2')  
            mapping = 2;
        end
        
        fld_par = fieldnames(data.(fld_con{c}).(fld_map{m}));
        for par=1:length(fld_par)
            temp = split(fld_par{par},'_');
            participant = str2double(temp{2});
            
%             M_yield = [M_yield; yield];
%             M_dis = [M_dis; distraction];
            M_map = [M_map; mapping];
            M_par = [M_par; participant];
            M_dc = [M_dc; data.(fld_con{c}).(fld_map{m}).(fld_par{par})];
        end
    end
end
M = [M_map, M_par, M_dc]; %[M_yield, M_dis, M_map, M_par, M_dc];
T = array2table(M);
% T.Properties.VariableNames(1:5) = {'yield','distraction','mapping','participant','decision_certainty'};
T.Properties.VariableNames(1:3) = {'mapping','participant','decision_certainty'};
writetable(T,tablename)

out = T;
end




