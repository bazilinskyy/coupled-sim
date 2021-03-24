%% Analyze Trial Order
% This function finds out the index of each participant and trial made by
% the createGroupData.

function out = analyzeTrialOrder(data)
orgdata = getOrganizedDY(data);
out = getAllNumber(orgdata);
end

%% Helperfunctions
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

function out = getNumber(data)
out.Pnr = zeros(1,length(data));
out.Tnr = zeros(1,length(data));
for i =1:length(data)
    splitstr = split(data{i},'_');
    out.Pnr(i) = str2double(splitstr(2));
    out.Tnr(i) = str2double(splitstr(4));
end
end
function out = getAllNumber(data)
fld_con = fieldnames(data);
for c=1:length(fld_con)
    fld_map = fieldnames(data.(fld_con{c}));
    for m=1:length(fld_map)
        out.(fld_con{c}).(fld_map{m}) = getNumber(data.(fld_con{c}).(fld_map{m}));
    end
end

end