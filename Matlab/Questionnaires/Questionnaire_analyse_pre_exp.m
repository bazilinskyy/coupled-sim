%% Questionnaire analyser - Pre-experiment
% This script reads in the questionnaire answers as a csv file and analyses 
% the data
% Author: Johnson Mok
% Last Updated: 05-01-2020
clc
close all
clear

%% Settings before running this script
ShowTables = true;
ShowPlots  = true;

%% Find questionnaire csv files
Folder   = join([cd,'\Questionnaire_Data']); %cd
FileList = dir(fullfile(Folder, '**', '*.csv'));

%% Pre-experiment Questionnaire (nr 5 in FileList)
filename    = join([FileList(5).folder,'\',FileList(5).name]);
opts        = delimitedTextImportOptions('Delimiter',',','DataLines', 2);
Preexp_Data = readmatrix(filename,opts);
% Split pedestrian and passenger
passenger_id    = [2,4,5,7,10,11,14,15,17,20,22];
pedestrian_id   = [1,3,6,8,9,12,13,16,18,19,21];
Preexp_pa       = Preexp_Data(passenger_id,:);
Preexp_pe       = Preexp_Data(pedestrian_id,:);
Data_preexp_pa  = convertDataPreexp(Preexp_pa);
Data_preexp_pe  = convertDataPreexp(Preexp_pe);

%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%% Calculations %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%

%% Calulations Pre-experiment questionnaire - Demographics
% Nationality
[Nationality_pa, Nat_name_pa, Nat_val_pa, stringName] = calcNationality(Data_preexp_pa.Nationality);
[Nationality_pe, Nat_name_pe, Nat_val_pe, ~] = calcNationality(Data_preexp_pa.Nationality);
T_Nat       = table([Nat_val_pa; Nat_val_pe],...
                'VariableNames',{stringName},...
                'RowNames',{'Passenger' 'Pedestrian'});
% Calculate mean and standard dev of age
mean_age_pa = mean(Data_preexp_pa.Age);
std_age_pa  = std(Data_preexp_pa.Age);
mean_age_pe = mean(Data_preexp_pe.Age);
std_age_pe  = std(Data_preexp_pe.Age);
T_age       = table([mean_age_pa; mean_age_pe], [std_age_pa; std_age_pe],...
                'VariableNames',{'Mean age' 'std age'},...
                'RowNames',{'Passenger' 'Pedestrian'});
% Calculate number of males and females
[male_pa, female_pa, pmale_pa, pfemale_pa] = calcGender(Data_preexp_pa.Gender);
[male_pe, female_pe, pmale_pe, pfemale_pe] = calcGender(Data_preexp_pe.Gender);
T_gender      = table([male_pa;male_pe], [female_pa;female_pe], [pmale_pa;pmale_pe], [pfemale_pa;pfemale_pe],...
                'VariableNames',{'nr male' 'nr female' 'percentage male' 'percentage female'},...
                'RowNames',{'Passenger' 'Pedestrian'});
% Seeing aids
[No_pa, Glasses_pa, Contacts_pa, pNo_pa, pGlasses_pa, pContacts_pa] = calcSeeingAid(Data_preexp_pa.Seeing_aids);
[No_pe, Glasses_pe, Contacts_pe, pNo_pe, pGlasses_pe, pContacts_pe] = calcSeeingAid(Data_preexp_pe.Seeing_aids);
T_seeingaid      = table([No_pa;No_pe], [Glasses_pa;Glasses_pe], [Contacts_pa;Contacts_pe],...
                         [pNo_pa;pNo_pe], [pGlasses_pa;pGlasses_pe], [pContacts_pa;pContacts_pe],...
                'VariableNames',{'No seeing aid' 'Glasses' 'Contacts' 'Percentage no seeing aid' 'Percentage glasses' 'Percentage contacts'},...
                'RowNames',{'Passenger' 'Pedestrian'});
if(ShowTables==true)
    disp(T_Nat);
    disp(T_age);
    disp(T_gender);
    disp(T_seeingaid);
end

%% Calulations Pre-experiment questionnaire - Experience
% Video game experience
[val_videoGameExp_pa, p_videoGameExp_pa] = calcVideoGameExp(Data_preexp_pa.VideoGameExp);
[val_videoGameExp_pe, p_videoGameExp_pe] = calcVideoGameExp(Data_preexp_pe.VideoGameExp);
T_videoGameExp = table([val_videoGameExp_pa(1);val_videoGameExp_pe(1)], ...
                        [val_videoGameExp_pa(2);val_videoGameExp_pe(2)], ...
                        [val_videoGameExp_pa(3);val_videoGameExp_pe(3)], ...
                        [val_videoGameExp_pa(4);val_videoGameExp_pe(4)], ...
                        [p_videoGameExp_pa(1);p_videoGameExp_pe(1)],...
                        [p_videoGameExp_pa(2);p_videoGameExp_pe(2)],...
                        [p_videoGameExp_pa(3);p_videoGameExp_pe(3)],...
                        [p_videoGameExp_pa(4);p_videoGameExp_pe(4)],...
                'VariableNames',{'No video game experience' 'Rarely' 'Weekly' 'Monthly' 'Percentage no video game experience' 'Percentage rarely' 'Percentage weekly' 'Percenteage monthly'},...
                'RowNames',{'Passenger' 'Pedestrian'});
% VR experience
[val_VRExp_pa, p_VRExp_pa] = calcVRExp(Data_preexp_pa.VRExp);
[val_VRExp_pe, p_VRExp_pe] = calcVRExp(Data_preexp_pe.VRExp);
T_VRExp = table([val_VRExp_pa(1);val_VRExp_pe(1)], ...
                [val_VRExp_pa(2);val_VRExp_pe(2)], ...
                [val_VRExp_pa(3);val_VRExp_pe(3)], ...
                [p_VRExp_pa(1);p_VRExp_pe(1)],...
                [p_VRExp_pa(2);p_VRExp_pe(2)],...
                [p_VRExp_pa(3);p_VRExp_pe(3)],...
                'VariableNames',{'No VR experience' 'Few' 'Often' 'Percentage no VR experience' 'Percentage few' 'Percentage often'},...
                'RowNames',{'Passenger' 'Pedestrian'});
% Crossing experiment experience
[val_CEExp_pa, p_CEExp_pa] = calcCrossingExperimentExp(Data_preexp_pa.CrossingExp);
[val_CEExp_pe, p_CEExp_pe] = calcCrossingExperimentExp(Data_preexp_pe.CrossingExp);
T_CEExp = table([val_CEExp_pa(1);val_CEExp_pe(1)], ...
                [val_CEExp_pa(2);val_CEExp_pe(2)], ...
                [p_CEExp_pa(1);p_CEExp_pe(1)],...
                [p_CEExp_pa(2);p_CEExp_pe(2)],...
                'VariableNames',{'No crossing experiment experience' 'Yes crossing experiment experience' 'Percentage no' 'Percentage yes'},...
                'RowNames',{'Passenger' 'Pedestrian'});
if(ShowTables==true)
    disp(T_videoGameExp);
    disp(T_VRExp);
    disp(T_CEExp);
end

%% Calulations Pre-experiment questionnaire - Driving Behaviour
% Driver's license
[GC_pa,GR_pa] = calcLicense(Data_preexp_pa.YearLicense);
[GC_pe,GR_pe] = calcLicense(Data_preexp_pe.YearLicense);

% Driving frequency
[driveFreq_pa, driveFreq_name_pa] = calcFreq(Data_preexp_pa.Driving_freq);
[driveFreq_pe, driveFreq_name_pe] = calcFreq(Data_preexp_pe.Driving_freq);

% calcDistanceDrive
[driveDis_pa, driveDis_name_pa] = calcDistanceDrive(Data_preexp_pa.Driving_distance);
[driveDis_pe, driveDis_name_pe] = calcDistanceDrive(Data_preexp_pe.Driving_distance);

% Driving accidents
mean_accidents_pa   = mean(Data_preexp_pa.Driving_accidents);
std_accidents_pa    = std(Data_preexp_pa.Driving_accidents);
mean_accidents_pe   = mean(Data_preexp_pe.Driving_accidents);
std_accidents_pe    = std(Data_preexp_pe.Driving_accidents);

% Driver eye contact - unsignalised
[driverUnsig_pa, driverUnsig_name_pa] = calcSevenLikertScale(Data_preexp_pa.Driver_EyeContact_unsignalised);
[driverUnsig_pe, driverUnsig_name_pe] = calcSevenLikertScale(Data_preexp_pe.Driver_EyeContact_unsignalised);

% Driver eye contact - zebra
[driverZebra_pa, driverZebra_name_pa] = calcSevenLikertScale(Data_preexp_pa.Driver_EyeContact_zebra);
[driverZebra_pe, driverZebra_name_pe] = calcSevenLikertScale(Data_preexp_pe.Driver_EyeContact_zebra);

% Driver eye contact - signalised
[driverSig_pa, driverSig_name_pa] = calcSevenLikertScale(Data_preexp_pa.Driver_EyeContact_signalised);
[driverSig_pe, driverSig_name_pe] = calcSevenLikertScale(Data_preexp_pe.Driver_EyeContact_signalised);

%% Calulations Pre-experiment questionnaire - Crossing Behaviour
% Frequency walking
[footFreq_pa, footFreq_name_pa] = calcFreq(Data_preexp_pa.Foot_freq);
[footFreq_pe, footFreq_name_pe] = calcFreq(Data_preexp_pe.Foot_freq);

% Pedestrian eye contact - unsignalised
[pedestrianUnsig_pa, pedestrianUnsig_name_pa] = calcSevenLikertScale(Data_preexp_pa.Pedestrian_EyeContact_unsignalised);
[pedestrianUnsig_pe, pedestrianUnsig_name_pe] = calcSevenLikertScale(Data_preexp_pe.Pedestrian_EyeContact_unsignalised);

% Pedestrian eye contact - zebra
[pedestrianZebra_pa, pedestrianZebra_name_pa] = calcSevenLikertScale(Data_preexp_pa.Pedestrian_EyeContact_zebra);
[pedestrianZebra_pe, pedestrianZebra_name_pe] = calcSevenLikertScale(Data_preexp_pe.Pedestrian_EyeContact_zebra);

% Pedestrian eye contact - signalised
[pedestrianSig_pa, pedestrianSig_name_pa] = calcSevenLikertScale(Data_preexp_pa.Pedestrian_EyeContact_signalised);
[pedestrianSig_pe, pedestrianSig_name_pe] = calcSevenLikertScale(Data_preexp_pe.Pedestrian_EyeContact_signalised);

%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%% Plots %%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%

%% Plots Pre-experiment questionnaire - Demographics
if(ShowPlots==true)
figure;
% Nationality
subplot(2,2,1);
X = categorical({'Passenger','Pedestrian'});
Y = [Nat_val_pa; Nat_val_pe];
h = bar(X,Y);
set(h, {'DisplayName'},{'Chinese','Dutch','Indian'}')
ylabel('Number of participants'); title('Nationalities'); legend();
% Age
subplot(2,2,2);
boxplot([Data_preexp_pa.Age; Data_preexp_pe.Age],[ones(size(Data_preexp_pa.Age)); 2*ones(size(Data_preexp_pe.Age))], ...
    'Labels',{'Passenger','Pedestrian'});
ylabel('Age'); title('Age of pedestrian and passenger');
% Gender
subplot(2,2,3);
X = categorical({'Passenger','Pedestrian'});
Y = [male_pa female_pa; male_pe female_pe];
h = bar(X,Y);
set(h, {'DisplayName'},{'Male','Female'}')
title('Gender'); ylabel('Number of participants'); legend();
% Seeing aid
subplot(2,2,4);
X = categorical({'Passenger','Pedestrian'});
Y = [No_pa Glasses_pa Contacts_pa; No_pe Glasses_pe Contacts_pe];
h = bar(X,Y);
set(h, {'DisplayName'},{'No seeing aid','Glasses','Contacts'}')
title('Seeing aids'); ylabel('Number of participants'); legend();

%% Plots Pre-experiment questionnaire - Experience
figure;
% Gaming experience
subplot(1,3,1);
X = categorical({'Passenger','Pedestrian'});
Y = [val_videoGameExp_pa; val_videoGameExp_pe];
h = bar(X,Y);
set(h, {'DisplayName'},{'No experience','Rarely','Weekly','Monthly'}')
title('Video game experience'); ylabel('Number of participants'); legend();
% VR experience
subplot(1,3,2);
X = categorical({'Passenger','Pedestrian'});
Y = [val_VRExp_pa; val_VRExp_pe];
h = bar(X,Y);
set(h, {'DisplayName'},{'No experience','Few','Often'}')
title('Virtual Reality experience'); ylabel('Number of participants'); legend();
% Crossing experiment experience
subplot(1,3,3);
X = categorical({'Passenger','Pedestrian'});
Y = [val_CEExp_pa; val_CEExp_pe];
h = bar(X,Y);
set(h, {'DisplayName'},{'No','Yes',}')
title('Crossing experiment experience'); ylabel('Number of participants'); legend();

%% Plots Pre-experiment questionnaire - Driving behaviour
figure
% Driver's license
subplot(1,2,1);
h       = bar(GR_pa,GC_pa);
xtips1  = GR_pa(1); ytips1 = GC_pa(1);
labels1 = "No License";
text(xtips1,ytips1,labels1,'HorizontalAlignment','center','VerticalAlignment','bottom')
h.FaceColor = 'flat'; h.CData(1,:) = [1 0 0];
title('Year driver license obtained - passenger'); ylabel('Number of participants');
subplot(1,2,2);
i = bar(GR_pe,GC_pe);
xtips2  = GR_pe(1); ytips2 = GC_pe(1);
labels2 = "No License";
text(xtips2,ytips2,labels2,'HorizontalAlignment','center','VerticalAlignment','bottom')
i.FaceColor = 'flat'; i.CData(1,:) = [1 0 0];
title('Year driver license obtained - pedestrian'); ylabel('Number of participants');

figure
% Driving frequency
subplot(1,3,1);
X = categorical(driveFreq_name_pa);
X = reordercats(X,driveFreq_name_pa);
h = bar(X,[driveFreq_pa; driveFreq_pe]);
set(h, {'DisplayName'},{'passenger','pedestrian',}')
title('Driving frequency'); ylabel('Number of participants'); legend();
% Driving distance
subplot(1,3,2);
X = categorical(driveDis_name_pa);
X = reordercats(X,driveDis_name_pa);
h = bar(X,[driveDis_pa; driveDis_pe]);
set(h, {'DisplayName'},{'passenger','pedestrian',}')
title('Driving distance'); ylabel('Number of participants'); legend();
% Driving accidents
subplot(1,3,3);
boxplot([Data_preexp_pa.Driving_accidents; Data_preexp_pe.Driving_accidents],[ones(size(Data_preexp_pa.Driving_accidents)); 2*ones(size(Data_preexp_pe.Driving_accidents))], ...
    'Labels',{'Passenger','Pedestrian'});
ylabel('Driving_accidents'); title('Driving accidents');

figure
% BOXPLOT - driver eyecontact - unsignalised
subplot(2,3,1);
boxplot([Data_preexp_pa.Driver_EyeContact_unsignalised; Data_preexp_pe.Driver_EyeContact_unsignalised],[ones(size(Data_preexp_pa.Driver_EyeContact_unsignalised)); 2*ones(size(Data_preexp_pe.Driver_EyeContact_unsignalised))], ...
    'Labels',{'Passenger','Pedestrian'});
ylabel('[Driver] eye-contact - Unsignalised'); title('Eye-contact searching as a driver at unsignalised crossings');
% BOXPLOT - driver eyecontact - zebra
subplot(2,3,2);
boxplot([Data_preexp_pa.Driver_EyeContact_zebra; Data_preexp_pe.Driver_EyeContact_zebra],[ones(size(Data_preexp_pa.Driver_EyeContact_zebra)); 2*ones(size(Data_preexp_pe.Driver_EyeContact_zebra))], ...
    'Labels',{'Passenger','Pedestrian'});
ylabel('[Driver] eye-contact - Zebra'); title('Eye-contact searching as a driver at zebra crossings');
% BOXPLOT - driver eyecontact - signalised
subplot(2,3,3);
boxplot([Data_preexp_pa.Driver_EyeContact_signalised; Data_preexp_pe.Driver_EyeContact_signalised],[ones(size(Data_preexp_pa.Driver_EyeContact_signalised)); 2*ones(size(Data_preexp_pe.Driver_EyeContact_signalised))], ...
    'Labels',{'Passenger','Pedestrian'});
ylabel('[Driver] eye-contact - Signalised'); title('Eye-contact searching as a driver at signalised crossings');
% driver eyecontact - unsignalised
subplot(2,3,4);
X = categorical(driverUnsig_name_pa);
X = reordercats(X,driverUnsig_name_pa);
h = bar(X,[driverUnsig_pa; driverUnsig_pe]);
set(h, {'DisplayName'},{'passenger','pedestrian',}')
title('[Driver] eye-contact - Unsignalised'); ylabel('Number of participants'); legend();
% driver eyecontact - zebra
subplot(2,3,5);
X = categorical(driverZebra_name_pa);
X = reordercats(X,driverZebra_name_pa);
h = bar(X,[driverZebra_pa; driverZebra_pe]);
set(h, {'DisplayName'},{'passenger','pedestrian',}')
title('[Driver] eye-contact - Zebra'); ylabel('Number of participants'); legend();
% driver eyecontact - signalised
subplot(2,3,6);
X = categorical(driverSig_name_pa);
X = reordercats(X,driverSig_name_pa);
h = bar(X,[driverSig_pa; driverSig_pe]);
set(h, {'DisplayName'},{'passenger','pedestrian',}')
title('[Driver] eye-contact - Signalised'); ylabel('Number of participants'); legend();

%% Plots Pre-experiment questionnaire - Crossing behaviour
figure
% Walking frequency
X = categorical(footFreq_name_pa);
X = reordercats(X,footFreq_name_pa);
h = bar(X,[footFreq_pa; footFreq_pe]);
set(h, {'DisplayName'},{'passenger','pedestrian',}')
title('Walking frequency'); ylabel('Number of participants'); legend();

figure
% BOXPLOT - pedestrian eyecontact - unsignalised
subplot(2,3,1);
boxplot([Data_preexp_pa.Pedestrian_EyeContact_unsignalised; Data_preexp_pe.Pedestrian_EyeContact_unsignalised],[ones(size(Data_preexp_pa.Pedestrian_EyeContact_unsignalised)); 2*ones(size(Data_preexp_pe.Pedestrian_EyeContact_unsignalised))], ...
    'Labels',{'Passenger','Pedestrian'});
ylabel('[Pedestrian] eye-contact - Unsignalised'); title('Eye-contact searching as a Pedestrian at unsignalised crossings');
% BOXPLOT - pedestrian eyecontact - zebra
subplot(2,3,2);
boxplot([Data_preexp_pa.Pedestrian_EyeContact_zebra; Data_preexp_pe.Pedestrian_EyeContact_zebra],[ones(size(Data_preexp_pa.Pedestrian_EyeContact_zebra)); 2*ones(size(Data_preexp_pe.Pedestrian_EyeContact_zebra))], ...
    'Labels',{'Passenger','Pedestrian'});
ylabel('[Pedestrian] eye-contact - Zebra'); title('Eye-contact searching as a Pedestrian at zebra crossings');
% BOXPLOT - pedestrian eyecontact - signalised
subplot(2,3,3);
boxplot([Data_preexp_pa.Pedestrian_EyeContact_signalised; Data_preexp_pe.Pedestrian_EyeContact_signalised],[ones(size(Data_preexp_pa.Pedestrian_EyeContact_signalised)); 2*ones(size(Data_preexp_pe.Pedestrian_EyeContact_signalised))], ...
    'Labels',{'Passenger','Pedestrian'});
ylabel('[Pedestrian] eye-contact - Signalised'); title('Eye-contact searching as a Pedestrian at signalised crossings');
% pedestrian eyecontact - unsignalised
subplot(2,3,4);
X = categorical(pedestrianUnsig_name_pa);
X = reordercats(X,pedestrianUnsig_name_pa);
h = bar(X,[pedestrianUnsig_pa; pedestrianUnsig_pe]);
set(h, {'DisplayName'},{'passenger','pedestrian',}')
title('[Pedestrian] eye-contact - Unsignalised'); ylabel('Number of participants'); legend();
% pedestrian eyecontact - zebra
subplot(2,3,5);
X = categorical(pedestrianZebra_name_pa);
X = reordercats(X,pedestrianZebra_name_pa);
h = bar(X,[pedestrianZebra_pa; pedestrianZebra_pe]);
set(h, {'DisplayName'},{'passenger','pedestrian',}')
title('[Pedestrian] eye-contact - Zebra'); ylabel('Number of participants'); legend();
% pedestrian eyecontact - signalised
subplot(2,3,6);
X = categorical(pedestrianSig_name_pa);
X = reordercats(X,pedestrianSig_name_pa);
h = bar(X,[pedestrianSig_pa; pedestrianSig_pe]);
set(h, {'DisplayName'},{'passenger','pedestrian',}')
title('[Pedestrian] eye-contact - Signalised'); ylabel('Number of participants'); legend();
end

%% Help functions Pre-experiment Questionnaire
function data = convertDataPreexp(inputcell)
% Demographics
data.Date            = inputcell(:,1);
data.Participant_nr  = str2double(inputcell(:,2));
data.Nationality     = inputcell(:,3);
data.Age             = str2double(inputcell(:,4));
data.Gender          = inputcell(:,5);
data.Seeing_aids     = inputcell(:,6);
% Experience
data.VideoGameExp    = inputcell(:,7);
data.VRExp           = inputcell(:,8);
data.CrossingExp     = inputcell(:,9);
% Driving behaviour
data.YearLicense                         = str2double(inputcell(:,10));
data.Driving_freq                        = inputcell(:,11);
data.Driving_distance                    = inputcell(:,12);
data.Driving_accidents                   = str2double(inputcell(:,13));
data.Driver_EyeContact_unsignalised     = str2double(inputcell(:,14));
data.Driver_EyeContact_zebra            = str2double(inputcell(:,15));
data.Driver_EyeContact_signalised       = str2double(inputcell(:,16));
% Crossing behaviour
% data.Foot_freq                               = inputcell(:,17);
data.Foot_freq                               = cellfun(@(S) S(1:end-1), inputcell(:,17), 'Uniform', 0);
data.Pedestrian_EyeContact_unsignalised     = str2double(inputcell(:,18));
data.Pedestrian_EyeContact_zebra            = str2double(inputcell(:,19));
data.Pedestrian_EyeContact_signalised       = str2double(inputcell(:,20));
end
% Demographics
function [male, female, percentageMale, percentageFemale] = calcGender(data)
male    = sum(strcmp(data, 'Male'));
female  = sum(strcmp(data, 'Female'));
Total   = length(data);
percentageMale = 100*male/Total;
percentageFemale = 100*female/Total; 
end
function rightNames = replaceWrongNames(data)
wrong_name  = ["Nederlandse","Nederlands","the Netherlands","nederalnds"];
wn_idx      = contains(data, wrong_name);
for i=1:length(data)
    if(wn_idx(i)==1)
       data{i} = 'Dutch';
    end
end
rightNames = data;
end
function [output, unique_nationality, sum_nationality, stringName] = calcNationality(data)
RightData           = replaceWrongNames(data);
unique_nationality  = unique(RightData);
sum_nationality = zeros(1,length(unique_nationality));
for i =1:length(unique_nationality)
    sum_nationality(i) = sum(strcmp(RightData, unique_nationality{i}));
end
output = [unique_nationality'; num2cell(sum_nationality)];
stringName = '';
for i=1:length(unique_nationality)
tempname = append(unique_nationality{i},' ');
    if i == length(unique_nationality)
        tempname = unique_nationality{i};
    end
stringName = append(stringName,tempname);
end
end
function [No, Glasses, Contacts, percentageNo, percentageGlasses, percentageContacts] = calcSeeingAid(data)
No = sum(strcmp(data, 'No'));
Glasses = sum(strcmp(data, 'Yes, glasses'));
Contacts = sum(strcmp(data, 'Yes, contact lenses'));
Total = length(data);
percentageNo = 100*No/Total;
percentageGlasses = 100*Glasses/Total; 
percentageContacts = 100*Contacts/Total; 
end
% Experience
function [val, perc] = calcVideoGameExp(data)
No      = sum(strcmp(data, 'No'));
Rarely  = sum(strcmp(data, 'Yes, I play/used to play rarely.'));
Monthly = sum(strcmp(data, 'Yes, I play/used to play several times a month.'));
Weekly  = sum(strcmp(data, 'Yes, I play/used to play several times a week.'));
Total   = length(data);
val     = [No, Rarely, Weekly, Monthly];
perc    = (100/Total).*val;
end
function [val, perc] = calcVRExp(data)
No      = sum(strcmp(data, 'No'));
Few     = sum(strcmp(data, 'Yes, I used it a few times.'));
Often   = sum(strcmp(data, 'Yes, I use it very often.'));
Total   = length(data);
val     = [No, Few, Often];
perc    = (100/Total).*val;
end
function [val, perc] = calcCrossingExperimentExp(data)
No      = sum(strcmp(data, 'No'));
Yes     = sum(strcmp(data, 'Yes'));
Total   = length(data);
val     = [No, Yes];
perc    = (100/Total).*val;
end
% Driving / Crossing behaviour
function [GC,GR] = calcLicense(data)
data(isnan(data)) = min(data)-1;
[GC,GR] = groupcounts(data); 
end
function [val, name] = calcFreq(data)
[GC,GR] = groupcounts(data); 
name = {'Every day'; '4 to 6 days a week'; '1 to 3 days a week';'Once a month to once a week';...
        'Less than once a month'; 'Never';'I prefer not to respond'};
val = zeros(1,length(name));
for i=1:length(name)
    index = find(strcmp(GR,name(i)));
    if(~isempty(index))
        val(i) = GC(index);
    end
end
end
function [val, name] = calcDistanceDrive(data)
[GC,GR] = groupcounts(data); 
name = {'0 km';'1 - 1.000 km';'1.001 - 5.000 km';'5.001 - 15.000 km';'15.001 - 20.000 km';...
    '20.001 - 25.000 km';'25.001 - 35.000 km';'35.001 - 50.000 km';'50.001 - 100.000 km';'More than 100.000 km';...
    'I prefer not to respond'};
val = zeros(1,length(name));
for i=1:length(name)
    index = find(strcmp(GR,name(i)));
    if(~isempty(index))
        val(i) = GC(index);
    end
end
end
function [val, name] = calcSevenLikertScale(data)
[GC,GR] = groupcounts(data); 
name = {'Extremely unlikely';'Unlikely'; 'Bit unlikely'; 'Neutral';'bit likely';'Likely';'Extremely likely'};
val = zeros(1,length(name));
for i=1:length(name)
    index = find(GR==i);
    if(~isempty(index))
        val(i) = GC(index);
    end
end
end
