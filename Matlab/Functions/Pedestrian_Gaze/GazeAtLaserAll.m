%% GazeAtLaser
% Calculate the time the pedestrian gazes at the passenger laser.
% Hierarchy: GazeAtLaserAll -> GazeAtLaser
% Author: Johnson Mok
function [out,out2] = GazeAtLaserAll(pe_world_gazeOrg, pe_world_gazeDir, pa_world_gazeOrg, pa_world_gazeDir, papos)
pe_org = getOrganisedData(pe_world_gazeOrg);
pe_dir = getOrganisedData(pe_world_gazeDir);
pa_org = getOrganisedData(pa_world_gazeOrg);
pa_dir = getOrganisedData(pa_world_gazeDir);
pa_pos = getOrganisedData(papos);

% Calculate index and time of laser gazing
fld_con = fieldnames(pe_org);
for c = 1:length(fld_con)
    fld_map = fieldnames(pe_org.(fld_con{c}));
    for m = 1:length(fld_map)
        out.(fld_con{c}).(fld_map{m}).gazeLaser = GazeAtLaser(pe_org.(fld_con{c}).(fld_map{m}),...
            pe_dir.(fld_con{c}).(fld_map{m}),...
            pa_org.(fld_con{c}).(fld_map{m}),...
            pa_dir.(fld_con{c}).(fld_map{m}),...
            pa_pos.(fld_con{c}).(fld_map{m}).pa_pos);
    end
end

% Create one big column array 
for c = 1:length(fld_con)
	for m = 1:length(fld_map)
        for i=1:length(out.(fld_con{c}).(fld_map{m}).gazeLaser.papos)
            papos_temp.(fld_con{c}).(fld_map{m}).gazeLaser.papos{i} = unique(round(out.(fld_con{c}).(fld_map{m}).gazeLaser.papos{i}));
        end
    end
end

% Calculate number of people gazing at laser at index/pos.
R = 1; % Round to R decimal.
for c = 1:length(fld_con)
	for m = 1:length(fld_map)
        Col = round(cat(1, papos_temp.(fld_con{c}).(fld_map{m}).gazeLaser.papos{:}),R);
        [temp_freq, out.(fld_con{c}).(fld_map{m}).val] = groupcounts(Col);
        out.(fld_con{c}).(fld_map{m}).freq = 100*temp_freq/(length(out.(fld_con{c}).(fld_map{m}).gazeLaser.papos));
    end
end

for c = 1:length(fld_con)
	for m = 1:length(fld_map)
        Col = cat(1, out.(fld_con{c}).(fld_map{m}).gazeLaser.index_intersect{:});
        [temp_freq, out2.(fld_con{c}).(fld_map{m}).val] = groupcounts(Col);
        out2.(fld_con{c}).(fld_map{m}).freq = 100*temp_freq/(length(out.(fld_con{c}).(fld_map{m}).gazeLaser.papos));
    end
end
end

%% Helper function
function out = getOrganisedData(data)
fld_xyz = fieldnames(data.Data_ED_0.HostFixedTimeLog); % lvl1 = 'eyeContact', 'pa', 'pe'
for p=1:length(fld_xyz)
    % ND_Y: ED 0, 4, 8
    ND_Y.map0.(fld_xyz{p}) = data.Data_ED_0.HostFixedTimeLog.(fld_xyz{p});
    ND_Y.map1.(fld_xyz{p}) = data.Data_ED_4.HostFixedTimeLog.(fld_xyz{p});
    ND_Y.map2.(fld_xyz{p}) = data.Data_ED_8.HostFixedTimeLog.(fld_xyz{p});
    % ND_NY: ED 1, 5, 9
    ND_NY.map0.(fld_xyz{p}) = data.Data_ED_1.HostFixedTimeLog.(fld_xyz{p});
    ND_NY.map1.(fld_xyz{p}) = data.Data_ED_5.HostFixedTimeLog.(fld_xyz{p});
    ND_NY.map2.(fld_xyz{p}) = data.Data_ED_9.HostFixedTimeLog.(fld_xyz{p});
    % D_Y: ED 2, 6, 10
    D_Y.map0.(fld_xyz{p}) = data.Data_ED_2.HostFixedTimeLog.(fld_xyz{p});
    D_Y.map1.(fld_xyz{p}) = data.Data_ED_6.HostFixedTimeLog.(fld_xyz{p});
    D_Y.map2.(fld_xyz{p}) = data.Data_ED_10.HostFixedTimeLog.(fld_xyz{p});
    % D_NY: ED 1, 5, 9
    D_NY.map0.(fld_xyz{p}) = data.Data_ED_3.HostFixedTimeLog.(fld_xyz{p});
    D_NY.map1.(fld_xyz{p}) = data.Data_ED_7.HostFixedTimeLog.(fld_xyz{p});
    D_NY.map2.(fld_xyz{p}) = data.Data_ED_11.HostFixedTimeLog.(fld_xyz{p});
end

out.ND_Y = ND_Y;
out.ND_NY = ND_NY;
out.D_Y = D_Y;
out.D_NY = D_NY;
end
