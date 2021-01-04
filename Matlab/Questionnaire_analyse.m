%% Questionnaire analyser
% This script reads in the questionnaire answers as a csv file and analyses 
% on the data
% Author: Johnson Mok
% Last Updated: 04-01-2020
clc
close all
clear

%% Find questionnaire csv files
Folder   = join([cd,'\Questionnaire_Data']); %cd
FileList = dir(fullfile(Folder, '**', '*.csv'));

%% Pre-experiment Questionnaire (nr 5 in FileList)
filename = join([FileList(5).folder,'\',FileList(5).name]);
opts = delimitedTextImportOptions('Delimiter',',',...
                                  'DataLines', 2);
Preexp_Data = readmatrix(filename,opts);

% Split pedestrian and passenger
passenger_id    = [2,4,5,7,10,11,14,15,17,20,22];
pedestrian_id   = [1,3,6,8,9,12,13,16,18,19,21];
Preexp_pa   = Preexp_Data(passenger_id,:);
Preexp_pe   = Preexp_Data(pedestrian_id,:);

Data_preexp_pa = convertDataPreexp(Preexp_pa);
Data_preexp_pe = convertDataPreexp(Preexp_pe);

%% Calulations
% Nationality
[Nationality_pa, Nat_name_pa, Nat_val_pa, stringName] = calcNationality(Data_preexp_pa.Nationality);
[Nationality_pe, Nat_name_pe, Nat_val_pe, ~] = calcNationality(Data_preexp_pa.Nationality);
T_age       = table([Nat_val_pa; Nat_val_pe],...
                'VariableNames',{stringName},...
                'RowNames',{'Passenger' 'Pedestrian'});
disp(T_age);

% Calculate mean and standard dev of age
mean_age_pa = mean(Data_preexp_pa.Age);
std_age_pa  = std(Data_preexp_pa.Age);
mean_age_pe = mean(Data_preexp_pe.Age);
std_age_pe  = std(Data_preexp_pe.Age);
T_age       = table([mean_age_pa; mean_age_pe], [std_age_pa; std_age_pe],...
                'VariableNames',{'Mean age' 'std age'},...
                'RowNames',{'Passenger' 'Pedestrian'});
disp(T_age);

% Calculate number of males and females
[male_pa, female_pa, pmale_pa, pfemale_pa] = calcGender(Data_preexp_pa.Gender);
[male_pe, female_pe, pmale_pe, pfemale_pe] = calcGender(Data_preexp_pe.Gender);
T_gender      = table([male_pa;male_pe], [female_pa;female_pe], [pmale_pa;pmale_pe], [pfemale_pa;pfemale_pe],...
                'VariableNames',{'nr male' 'nr female' 'percentage male' 'percentage female'},...
                'RowNames',{'Passenger' 'Pedestrian'});
disp(T_gender);

% Seeing aids
[No_pa, Glasses_pa, Contacts_pa, pNo_pa, pGlasses_pa, pContacts_pa] = calcSeeingAid(Data_preexp_pa.Seeing_aids);
[No_pe, Glasses_pe, Contacts_pe, pNo_pe, pGlasses_pe, pContacts_pe] = calcSeeingAid(Data_preexp_pe.Seeing_aids);
T_seeingaid      = table([No_pa;No_pe], [Glasses_pa;Glasses_pe], [Contacts_pa;Contacts_pe],...
                         [pNo_pa;pNo_pe], [pGlasses_pa;pGlasses_pe], [pContacts_pa;pContacts_pe],...
                'VariableNames',{'No seeing aid' 'Glasses' 'Contacts' 'Percentage no seeing aid' 'Percentage glasses' 'Percentage contacts'},...
                'RowNames',{'Passenger' 'Pedestrian'});
disp(T_seeingaid);

%% Plots
% Nationality
figure;
subplot(2,2,1);
X = categorical({'Passenger','Pedestrian'});
Y = [Nat_val_pa; Nat_val_pe];
h = bar(X,Y);
set(h, {'DisplayName'},{'Chinese','Dutch','Indian'}')
ylabel('Number of participants');
title('Nationalities');
legend()

% Age
% figure;
subplot(2,2,2);
boxplot([Data_preexp_pa.Age; Data_preexp_pe.Age],[ones(size(Data_preexp_pa.Age)); 2*ones(size(Data_preexp_pe.Age))], ...
    'Labels',{'Passenger','Pedestrian'});
ylabel('Age')
title('Age of pedestrian and passenger')

% Gender
% figure;
subplot(2,2,3);
X = categorical({'Passenger','Pedestrian'});
Y = [male_pa female_pa; male_pe female_pe];
h = bar(X,Y);
set(h, {'DisplayName'},{'Male','Female'}')
title('Gender');
ylabel('Number of participants');
legend()

% Seeing aid
% figure;
subplot(2,2,4);
X = categorical({'Passenger','Pedestrian'});
Y = [No_pa Glasses_pa Contacts_pa; No_pe Glasses_pe Contacts_pe];
h = bar(X,Y);
set(h, {'DisplayName'},{'No seeing aid','Glasses','Contacts'}')
title('Seeing aids');
ylabel('Number of participants');
legend()

%% Help functions
% Turn into data into data structure
function data = convertDataPreexp(inputcell)
% Demographics
data.Date            = inputcell(:,1);
data.Participant_nr  = inputcell(:,2);
data.Nationality     = inputcell(:,3);
data.Age             = str2double(inputcell(:,4));
data.Gender          = inputcell(:,5);
data.Seeing_aids     = inputcell(:,6);
% Experience
data.VideoGameExp    = inputcell(:,7);
data.VRExp           = inputcell(:,8);
data.CrossingExp     = inputcell(:,9);
% Driving behaviour
data.YearLicense                         = inputcell(:,10);
data.Driving_freq                        = inputcell(:,11);
data.Driving_distance                    = inputcell(:,12);
data.Driving_accidents                   = inputcell(:,13);
data.Driver_EyeContract_unsignalised     = inputcell(:,14);
data.Driver_EyeContract_zebra            = inputcell(:,15);
data.Driver_EyeContract_signalised       = inputcell(:,16);
% Crossing behaviour
data.Foot_freq                               = inputcell(:,17);
data.Pedestrian_EyeContract_unsignalised     = inputcell(:,18);
data.Pedestrian_EyeContract_zebra            = inputcell(:,19);
data.Pedestrian_EyeContract_signalised       = inputcell(:,20);
end
function [male, female, percentageMale, percentageFemale] = calcGender(data)
male = sum(strcmp(data, 'Male'));
female = sum(strcmp(data, 'Female'));
Total = length(data);
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


