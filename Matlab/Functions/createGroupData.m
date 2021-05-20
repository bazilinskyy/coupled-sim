%% CreateGroupData
% This script takes variables from individuals and puts those values in a long
% array, effectively creating an array of that variable.
% Author: Johnson Mok

function output = createGroupData(AllData, var)
%% create struct to input the data
fields_ED = fieldnames(AllData);
for j = 1:length(fields_ED)
    fields_time = fieldnames(AllData.(fields_ED{j}));
    for k = 1:length(fields_time)
        fields_participants = fieldnames(AllData.(fields_ED{j}).(fields_time{k}));
        if(strcmp(var,'time'))
            output.(fields_ED{j}).(fields_time{k}).eyeContact = []; %
        elseif(strcmp(var,'gap'))
            output.(fields_ED{j}).(fields_time{k}).gapAcceptance = {}; %
        elseif(strcmp(var,'rb_v'))
            output.(fields_ED{j}).(fields_time{k}).rb_v_z = {}; %
        elseif(strcmp(var,'pa_pos'))
            output.(fields_ED{j}).(fields_time{k}).pa_pos = {}; %
        elseif(strcmp(var,'pa_distance'))
            output.(fields_ED{j}).(fields_time{k}).pa_distance = {}; %
        elseif(strcmp(var,'pe_distance'))
            output.(fields_ED{j}).(fields_time{k}).pe_distance = {}; %
        elseif(strcmp(var,'phases'))
            output.(fields_ED{j}).(fields_time{k}) = {}; %
        elseif(strcmp(var,'pe_rotation'))
            output.(fields_ED{j}).(fields_time{k}).x = {}; %
            output.(fields_ED{j}).(fields_time{k}).y = {}; %
            output.(fields_ED{j}).(fields_time{k}).z = {}; %
        elseif(strcmp(var,'HMDgaze_dir'))
            output.(fields_ED{j}).(fields_time{k}).x = {}; %
            output.(fields_ED{j}).(fields_time{k}).y = {}; %
            output.(fields_ED{j}).(fields_time{k}).z = {}; %
        elseif(strcmp(var,'HMDgaze_org'))
            output.(fields_ED{j}).(fields_time{k}).x = {}; %
            output.(fields_ED{j}).(fields_time{k}).y = {}; %
            output.(fields_ED{j}).(fields_time{k}).z = {}; %
        elseif(strcmp(var,'trialorder'))
            output.(fields_ED{j}) = {}; %
        elseif(strcmp(var,'pa_world_gaze_dir'))
            output.(fields_ED{j}).(fields_time{k}).x = {}; %
            output.(fields_ED{j}).(fields_time{k}).y = {}; %
            output.(fields_ED{j}).(fields_time{k}).z = {}; %
        elseif(strcmp(var,'pa_world_gaze_org'))
            output.(fields_ED{j}).(fields_time{k}).x = {}; %
            output.(fields_ED{j}).(fields_time{k}).y = {}; %
            output.(fields_ED{j}).(fields_time{k}).z = {}; %
        elseif(strcmp(var,'pe_world_gaze_dir'))
            output.(fields_ED{j}).(fields_time{k}).x = {}; %
            output.(fields_ED{j}).(fields_time{k}).y = {}; %
            output.(fields_ED{j}).(fields_time{k}).z = {}; %
        elseif(strcmp(var,'pe_world_gaze_org'))
            output.(fields_ED{j}).(fields_time{k}).x = {}; %
            output.(fields_ED{j}).(fields_time{k}).y = {}; %
            output.(fields_ED{j}).(fields_time{k}).z = {}; %
        end
        
        for idx = 1:length(fields_participants)
            fields_trials = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}));
            for i = 1:length(fields_trials)
                % Part for time
                if(strcmp(var,'time'))
                    output.(fields_ED{j}).(fields_time{k}).eyeContact(end+1) = AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).eyeContact;
                    fields_T = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}));
                    idx_TPa = find(strcmp(fields_T,'pa'));
                    idx_TPe = find(strcmp(fields_T,'pe'));
                    for l = [idx_TPa, idx_TPe]
                        fields_phase = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).(fields_T{l}));
                        if(~isfield(output.(fields_ED{j}).(fields_time{k}),(fields_T{l})))
                            output.(fields_ED{j}).(fields_time{k}).(fields_T{l}) = [];
                        end
                        for m = 1:length(fields_phase)
                            if(~isfield(output.(fields_ED{j}).(fields_time{k}).(fields_T{l}),(fields_phase{m})))
                                output.(fields_ED{j}).(fields_time{k}).(fields_T{l}).(fields_phase{m}) = [];
                            end
                            fields_var = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).(fields_T{l}).(fields_phase{m}));
                            for n = 1:length(fields_var)
                                idx_dis = find(strcmp(fields_var,'distance'));
                                if(n~=idx_dis)
                                    if(~isfield(output.(fields_ED{j}).(fields_time{k}).(fields_T{l}).(fields_phase{m}),(fields_var{n})))
                                        output.(fields_ED{j}).(fields_time{k}).(fields_T{l}).(fields_phase{m}).(fields_var{n}) = [];
                                    end
                                    output.(fields_ED{j}).(fields_time{k}).(fields_T{l}).(fields_phase{m}).(fields_var{n}) = [output.(fields_ED{j}).(fields_time{k}).(fields_T{l}).(fields_phase{m}).(fields_var{n}), AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).(fields_T{l}).(fields_phase{m}).(fields_var{n})];
                                end
                                if(n==idx_dis)
                                	if(~isfield(output.(fields_ED{j}).(fields_time{k}).(fields_T{l}).(fields_phase{m}),(fields_var{n})))
                                        output.(fields_ED{j}).(fields_time{k}).(fields_T{l}).(fields_phase{m}).(fields_var{n}) = [];
                                    end  
                                    output.(fields_ED{j}).(fields_time{k}).(fields_T{l}).(fields_phase{m}).(fields_var{n}) = [output.(fields_ED{j}).(fields_time{k}).(fields_T{l}).(fields_phase{m}).(fields_var{n}), {AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).(fields_T{l}).(fields_phase{m}).(fields_var{n})}];
                                end
                            end
                        end
                    end
                end
                % Part for gapAcceptance
                if(strcmp(var,'gap'))
                    output.(fields_ED{j}).(fields_time{k}).gapAcceptance(end+1,:) = {AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pe.gapAcceptance};
                end
                % Part for AV velocity
                if(strcmp(var,'rb_v'))
                    output.(fields_ED{j}).(fields_time{k}).rb_v_z(end+1,:) = {AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pa.world.rb_v.z};
                end
                % Part for AV position
                if(strcmp(var,'pa_pos'))
                    output.(fields_ED{j}).(fields_time{k}).pa_pos(end+1,:) = {AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pa.pos.z};
                end
                % Part for passenger distance
                if(strcmp(var,'pa_distance'))
                    output.(fields_ED{j}).(fields_time{k}).pa_distance(end+1,:) = {AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pa.distance};
                end
                % Part for pedestrian distance
                if(strcmp(var,'pe_distance'))
                    output.(fields_ED{j}).(fields_time{k}).pe_distance(end+1,:) = {AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pe.distance};
                end
                % Part for phases
                if(strcmp(var,'phases'))
                    fld = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}));
                    for i_ph = 1:3 %%%%% wip
                        if(~isfield(output.(fields_ED{j}).(fields_time{k}),(fld{i_ph})))
                            output.(fields_ED{j}).(fields_time{k}).(fld{i_ph}) = [];
                        end
                    end
                    for i_ph = 1:2
                        output.(fields_ED{j}).(fields_time{k}).(fld{i_ph}) = [output.(fields_ED{j}).(fields_time{k}).(fld{i_ph}), {AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).(fld{i_ph})}];
                    end
                    fldpos = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pos);
                    for i_pos = 1:length(fldpos)
                    	if(~isfield(output.(fields_ED{j}).(fields_time{k}).(fld{3}),(fldpos{i_pos})))
                            output.(fields_ED{j}).(fields_time{k}).(fld{3}).(fldpos{i_pos}) = [];
                        end
                        output.(fields_ED{j}).(fields_time{k}).(fld{3}).(fldpos{i_pos}) = [output.(fields_ED{j}).(fields_time{k}).(fld{3}).(fldpos{i_pos}),...
                    	{AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).(fld{3}).(fldpos{i_pos})}];
                    end
                end
                % Part for pedestrian rotation
                if(strcmp(var,'pe_rotation'))
                    fld_dir = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pe.rot);
                    for d=1:length(fld_dir)
                        output.(fields_ED{j}).(fields_time{k}).(fld_dir{d})(end+1,:) = {AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pe.rot.(fld_dir{d})};
                    end
                end
                if(strcmp(var,'HMDgaze_dir'))
                    fld_dir = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pe.HMD.gaze_dir);
                    for d=1:length(fld_dir)
                        output.(fields_ED{j}).(fields_time{k}).(fld_dir{d})(end+1,:) = {AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pe.HMD.gaze_dir.(fld_dir{d})};
                    end
                end
                if(strcmp(var,'HMDgaze_org'))
                    fld_dir = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pe.HMD.gaze_origin);
                    for d=1:length(fld_dir)
                        output.(fields_ED{j}).(fields_time{k}).(fld_dir{d})(end+1,:) = {AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pe.HMD.gaze_origin.(fld_dir{d})};
                    end
                end
                if(strcmp(var,'trialorder'))
                    output.(fields_ED{j}){end+1} = [fields_participants{idx},'_',fields_trials{i}];
                end
                if(strcmp(var,'pa_world_gaze_dir'))
                    fld_dir = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pe.HMD.gaze_origin);
                    for d=1:length(fld_dir)
                        output.(fields_ED{j}).(fields_time{k}).(fld_dir{d})(end+1,:) = {AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pa.world.gaze_dir.(fld_dir{d})};
                    end
                end
                if(strcmp(var,'pa_world_gaze_org'))
                    fld_dir = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pe.HMD.gaze_origin);
                    for d=1:length(fld_dir)
                        output.(fields_ED{j}).(fields_time{k}).(fld_dir{d})(end+1,:) = {AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pa.world.gaze_origin.(fld_dir{d})};
                    end
                end
                if(strcmp(var,'pe_world_gaze_dir'))
                    fld_dir = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pe.HMD.gaze_origin);
                    for d=1:length(fld_dir)
                        output.(fields_ED{j}).(fields_time{k}).(fld_dir{d})(end+1,:) = {AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pe.world.gaze_dir.(fld_dir{d})};
                    end
                end
                if(strcmp(var,'pe_world_gaze_org'))
                    fld_dir = fieldnames(AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pe.HMD.gaze_origin);
                    for d=1:length(fld_dir)
                        output.(fields_ED{j}).(fields_time{k}).(fld_dir{d})(end+1,:) = {AllData.(fields_ED{j}).(fields_time{k}).(fields_participants{idx}).(fields_trials{i}).pe.world.gaze_origin.(fld_dir{d})};
                    end
                end
            end
        end
    end
end
end
