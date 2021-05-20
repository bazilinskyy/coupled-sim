%% GazeAtAVAll
% Calculate the time the pedestrian gazes at the AV
% Author: Johnson Mok

function out = GazeAtAVAll(data)
orgdata = getOrganisedData(data);

fld_con = fieldnames(orgdata);
for c = 1:length(fld_con)
    fld_map = fieldnames(orgdata.(fld_con{c}));
    for m = 1:length(fld_map)
        out.(fld_con{c}).(fld_map{m}) = GazeAtAV(orgdata.(fld_con{c}).(fld_map{m}));
    end
end

end

%% Helper function
function out = getOrganisedData(data)
    % ND_Y: ED 0, 4, 8
    ND_Y.map0 = data.Data_ED_0.HostFixedTimeLog.pe_distance;
    ND_Y.map1 = data.Data_ED_4.HostFixedTimeLog.pe_distance;
    ND_Y.map2 = data.Data_ED_8.HostFixedTimeLog.pe_distance;
    % ND_NY: ED 1, 5, 9
    ND_NY.map0 = data.Data_ED_1.HostFixedTimeLog.pe_distance;
    ND_NY.map1 = data.Data_ED_5.HostFixedTimeLog.pe_distance;
    ND_NY.map2 = data.Data_ED_9.HostFixedTimeLog.pe_distance;
    % D_Y: ED 2, 6, 10
    D_Y.map0 = data.Data_ED_2.HostFixedTimeLog.pe_distance;
    D_Y.map1 = data.Data_ED_6.HostFixedTimeLog.pe_distance;
    D_Y.map2 = data.Data_ED_10.HostFixedTimeLog.pe_distance;
    % D_NY: ED 1, 5, 9
    D_NY.map0 = data.Data_ED_3.HostFixedTimeLog.pe_distance;
    D_NY.map1 = data.Data_ED_7.HostFixedTimeLog.pe_distance;
    D_NY.map2 = data.Data_ED_11.HostFixedTimeLog.pe_distance;


out.ND_Y = ND_Y;
out.ND_NY = ND_NY;
out.D_Y = D_Y;
out.D_NY = D_NY;
end
