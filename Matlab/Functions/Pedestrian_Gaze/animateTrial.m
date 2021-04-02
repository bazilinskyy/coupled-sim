%% analyzePedestrianGazeDirection
% This script ...
% From the pedestrians POV:
% positive z is right, negative z is left
% positive x is in front, negative x is in back
% positive y is up, negative y is down
% rot is in degrees
% Author: Johnson Mok
% Last Updated: 19-02-2020


function animateTrial(gazeorg, gazedir ,gap, pagazeorg, pagazedir, paLook, videoname, titlestr)
opengl hardware
%% Prepare data
dt = 0.0167;
% Animation trial - pedestrian
dir = RemoveNoTracking(gazedir);
org = RemoveNoTracking(gazeorg);
dirP = DirectionViewPoint(org,dir,50);
% Animation trial - passenger
padir = RemoveNoTracking(pagazedir);
paorg = RemoveNoTracking(pagazeorg);
padirP = DirectionViewPoint(paorg,padir,50);
[startstop, endstop, endPhase3] = findStandstill(paorg);
[PatchAni,xp] = getPatchAni(paorg.z, startstop, endstop, endPhase3);
% midxAni = getMidxAni(paorg.z, startstop, endstop);
% Gap acceptance
rectgap = getRect(paorg.z, startstop, endstop, endPhase3, 1);
midxgap = getMidx(paorg.z, startstop, endstop, endPhase3);
% Passenger gaze distance
paLook(paLook == -1) = NaN;
rectdis = getRect(paorg.z, startstop, endstop, endPhase3, 60);
midxdis = getMidx(paorg.z, startstop, endstop, endPhase3);
% Other
customColor = [0, 0.4470, 0.7410;...
    0.0, 0.5, 0.0;...
    0.65, 0.16, 0.16;...
    0.4940, 0.1840, 0.5560];
time = (1:length(gap))*dt;

%% Initialize video
myVideo = VideoWriter(videoname, 'Uncompressed AVI'); %open video file
% myVideo.Quality = 100;
myVideo.FrameRate = 59;  %can adjust this, 5 - 10 works well for me
open(myVideo)

%% Pedestrian gaze animation
figure
subplot(2,2,[1 3])
% road
hold on
plot3([-115.1, -115.1], [0,0], [-100, 100],'--', 'LineWidth', 1, 'Color', 'k');
plot3([-105.1, -105.1], [0,0], [-100, 100],'--', 'LineWidth', 1, 'Color', 'k');
% zebra
% plot3([-115.1, -105.1],[0, 0],[20.1, 20.1],'--' ,'LineWidth', 1, 'Color', 'k');
% plot3([-115.1, -105.1],[0, 0],[15.35, 15.35],'--', 'LineWidth', 1, 'Color', 'k');
h1 = plot3(NaN, NaN, NaN, '-','Color',customColor(1,:));                                              % pedestrian gaze line
% hold on
h2 = plot3(NaN, NaN, NaN, 'o','MarkerFaceColor',customColor(1,:),'MarkerEdgeColor',customColor(1,:));   % pedestrian position
h3 = plot3(NaN, NaN, NaN, '-','Color',customColor(2,:));                                              % passenger gaze line
h4 = plot3(NaN, NaN, NaN, 's','MarkerSize',6,'MarkerFaceColor',customColor(2,:),'MarkerEdgeColor',customColor(2,:)); % passenger position
axis([-120 -100 -5 5 -40 80]);
view(0,0) % XZ
% set(gca,'xticklabel',[],'yticklabel',[],'zticklabel',[])
set(gca,'xticklabel',[])
% zticks([(-40:10:60)+17.19]);
% zticklabels({'-40','-30','-20','-10','0','10','20','30','40','50','60'});
zticks([(-40:20:60)+17.19]);
zticklabels({'-40','-20','0','20','40','60'});
title(titlestr,'FontSize',15,'FontWeight','bold');
grid on;
zlabel('Distance from pedestrian in [m]','FontSize',12,'FontWeight','bold');
axis manual %// this line freezes the axes
for i=1:size(PatchAni,1)
     patch('XData', xp, 'YData', zeros(4,1), 'ZData', PatchAni(i,:),...
         'FaceColor',[0.9-i/10 0.9-i/10 0.9-i/10]);
     alpha(0.4);
end

%% Phases
% line([-120 -100],[0 0],[76.94 76.94],'LineStyle','--','Color','k'); 
text(-100,0,76.94,'(phase 1)','HorizontalAlignment','right','VerticalAlignment','top');

% line([-120 -100],[0 0],[42.19 42.19],'LineStyle','--','Color','k'); 
text(-100,0,42.19,'(phase 2)','HorizontalAlignment','right','VerticalAlignment','top');

% line([-120 -100],[0 0],[31.59 31.59],'LineStyle','--','Color','k'); 
% text(-100,0,31.59,'(phase 3)','HorizontalAlignment','right','VerticalAlignment','top');
text(-100,0,paorg.z(startstop),'(phase 3)','HorizontalAlignment','right','VerticalAlignment','top');

% line([-120 -100],[0 0],[paorg.z(startstop) paorg.z(startstop)],'LineStyle','--','Color','k'); 
% text(-100,0,paorg.z(startstop),{'(line = phase 4';'phase 5)'},'HorizontalAlignment','right','VerticalAlignment','top');

% line([-120 -100],[0 0],[12.5 12.5],'LineStyle','--','Color','k'); 
% text(-100,0,12.5,'(end of phases)','HorizontalAlignment','right','VerticalAlignment','top');

%% Gap acceptance
subplot(2,2,2) 
for r=1:size(rectgap,1)
    rectangle('Position', rectgap(r,:),'FaceColor',[0.9-r/10 0.9-r/10 0.9-r/10 0.4],'EdgeColor',[0 0 0])
    text(midxgap(r), 1,['(',num2str(r),')'],'HorizontalAlignment','center','VerticalAlignment', 'top');
end
g1 = animatedline('LineWidth', 2,'Color',customColor(3,:)); % Gap acceptance
hold on
g2 = plot(NaN, NaN, 'o','MarkerSize',6,'MarkerEdgeColor','k','MarkerFaceColor','k');  
xlabel('time in [s]','FontSize',12,'FontWeight','bold');
ylabel('Feels safe to cross','FontSize',12,'FontWeight','bold');
grid on;
yticks([0 1])
yticklabels({'No','Yes'})
axis([0 21 0 1]);

%% Passenger gazing pedestrian distance
subplot(2,2,4) 
for r=1:size(rectdis,1)
    rectangle('Position', rectdis(r,:),'FaceColor',[0.9-r/10 0.9-r/10 0.9-r/10 0.4],'EdgeColor',[0 0 0])
    text(midxdis(r), 60,['(',num2str(r),')'],'HorizontalAlignment','center','VerticalAlignment', 'top');
end
l1 = animatedline('LineWidth', 2,'Color',customColor(4,:)); % pa look distance
hold on
l2 = plot(NaN, NaN, 'o','MarkerSize',6,'MarkerEdgeColor','k','MarkerFaceColor','k');  
xlabel('time in [s]','FontSize',12,'FontWeight','bold');
ylabel({'Distance passenger looking';'at pedestrian in [m]'},'FontSize',12,'FontWeight','bold');
grid on;
axis([0 21 0 60]);

%% Animation
for n = 1:length(dir.x)
    set(h1, 'XData', [org.x(n), dirP.x(n)], 'YData', [org.y(n), dirP.y(n)], 'ZData', [org.z(n), dirP.z(n)]);
    set(h2, 'XData', org.x(n), 'YData', org.y(n), 'ZData', org.z(n));
    
    set(h3, 'XData', [paorg.x(n), padirP.x(n)], 'YData', [paorg.y(n), padirP.y(n)], 'ZData', [paorg.z(n), padirP.z(n)]);
    set(h4, 'XData', paorg.x(n), 'YData', paorg.y(n), 'ZData', paorg.z(n));
    
    addpoints(g1, time(n), gap(n));
    set(g2, 'XData', time(n), 'YData', gap(n));
        
    addpoints(l1, time(n), paLook(n));
    set(l2, 'XData', time(n), 'YData', paLook(n));

    drawnow
    pause(dt)
    
    frame = getframe(gcf); %get frame
    writeVideo(myVideo, frame);
end

close(myVideo)

%% convert AVI to MP4
% pathVideoMP4 = regexprep(videoname,'\.avi','.mp4'); % generate mp4 filename
% if isunix % for linux
%     [~,~] = system(sprintf('ffmpeg -i %s -y -an -c:v libx264 -crf 0 -preset slow %s',videoname,pathVideoMP4)); % for this to work, you should have installed ffmpeg and have it available on PATH
% elseif ispc % for windows
%     [~,~] = system(sprintf('ffmpeg.exe -i %s -y -an -c:v libx264 -crf 0 -preset slow %s',videoname,pathVideoMP4)); % for this to work, you should have installed ffmpeg and have it available on PATH
% end
end

%% Helper function
function out = RemoveNoTracking(data)
fld_coor = fieldnames(data);
for coor = 1:length(fld_coor)
        data.(fld_coor{coor})(data.(fld_coor{coor}) == -1) = NaN;
end
out = data;
end

function out = DirectionViewPoint(org,dir,L)
fld_coor = fieldnames(dir);
for c = 1:length(fld_coor)
    out.(fld_coor{c}) = org.(fld_coor{c})+dir.(fld_coor{c})*L;
end
end

function [startsmooth, endsmooth, standstilltwosix] = findStandstill(data)
a = round(4/0.0167)+40;
v = abs(gradient(data.z));
startstop = find(v(450:end)<0.001, 1, 'first')+450;
endstop = find(v(1:startstop+a)<0.001, 1, 'last');
v_smooth = abs(smooth(v,8));
startsmooth = find(v_smooth<0.001, 1, 'first');
endsmooth = find(v_smooth<0.001, 1, 'last');
standstilltwosix = startsmooth + round(2.6/0.0167);
if standstilltwosix > endsmooth
    standstilltwosix = endsmooth;
end
% Debug
figure
subplot(2,1,1)
plot(data.z);
hold on;
xline(startstop);
xline(endstop);
subplot(2,1,2);
hold on;
plot(v);
xline(startstop);
xline(endstop);

figure
subplot(2,1,1)
plot(data.z);
hold on;
xline(startsmooth);
xline(endsmooth);
subplot(2,1,2);
hold on;
plot(v_smooth);
xline(startsmooth);
xline(endsmooth);
end

function out = getRect(pos, startstop, endstop, standstilltwosix, height)
dt = 0.0167;
phases = 3;
if (phases == 3)
    i1_start = find(pos<=76.94,1,'first');  % startsound
    i1_end = find(pos<=42.19,1,'first');    % 25m
    i2_start = i1_end;                      % 25m
    i2_end = startstop;                     % standstill
    i3_start = i2_end;
    i3_end = standstilltwosix;              % standstill for 2.6 sec

    p1 = [i1_start*dt 0 (i1_end-i1_start)*dt height];
    p2 = [i2_start*dt 0 (i2_end-i2_start)*dt height];
    p3 = [i3_start*dt 0 (i3_end-i3_start)*dt height];

    out = [p1; p2; p3]; 
end
if(phases == 5)
    i1_start = find(pos<=76.94,1,'first');
    i1_end = find(pos<=42.19,1,'first');
    i2_start = i1_end;
    i2_end = find(pos<=31.59,1,'first');
    i3_start = i2_end;
    i3_end = startstop;
    i4_start = i3_end;
    i4_end = endstop;
    i5_start = i4_end;
    i5_end = find(pos<=12.5,1,'first');

    p1 = [i1_start*dt 0 (i1_end-i1_start)*dt height];
    p2 = [i2_start*dt 0 (i2_end-i2_start)*dt height];
    p3 = [i3_start*dt 0 (i3_end-i3_start)*dt height];
    p4 = [i4_start*dt 0 (i4_end-i4_start)*dt height];
    p5 = [i5_start*dt 0 (i5_end-i5_start)*dt height];

    out = [p1; p2; p3; p4; p5];
end
end
function out = getMidx(pos, startstop, endstop, standtwosix)
dt = 0.0167;
phases = 3;
if (phases == 3)
    i1_start = find(pos<=76.94,1,'first');  % start sound
    i1_end = find(pos<=42.19,1,'first');    % 25m
    i2_start = i1_end;                      % 25m
    i2_end = startstop;                     % standstill
    i3_start = i2_end;                      % standstill
    i3_end = standtwosix;                   % standstill 3.6s


    p1 = (i1_start + (i1_end-i1_start)/2)*dt;
    p2 = (i2_start + (i2_end-i2_start)/2)*dt;
    p3 = (i3_start + (i3_end-i3_start)/2)*dt;
    
    out = [p1; p2; p3];
elseif (phases == 5)
    i1_start = find(pos<=76.94,1,'first');
    i1_end = find(pos<=42.19,1,'first');
    i2_start = i1_end;
    i2_end = find(pos<=31.59,1,'first');
    i3_start = i2_end;
    i3_end = startstop;
    i4_start = i3_end;
    i4_end = endstop;
    i5_start = i4_end;
    i5_end = find(pos<=12.5,1,'first');

    p1 = (i1_start + (i1_end-i1_start)/2)*dt;
    p2 = (i2_start + (i2_end-i2_start)/2)*dt;
    p3 = (i3_start + (i3_end-i3_start)/2)*dt;
    p4 = (i4_start + (i4_end-i4_start)/2)*dt;
    p5 = (i5_start + (i5_end-i5_start)/2)*dt;
    
    out = [p1; p2; p3; p4; p5];
end

end

function [out,xp] = getPatchAni(pos, startstop, endstop, i3_end)
phases = 3;
if (phases == 3)
    i1_start = 76.94;   % startsound
    i1_end = 42.19;     % 25m
    i2_start = i1_end;  % 25m
    i2_end = pos(startstop);
    i3_start = i2_end;
    i3_end = pos(i3_end);
    % z values
    p1 = [i1_start i1_end i1_end i1_start];
    p2 = [i2_start i2_end i2_end i2_start];
    p3 = [i3_start i3_end i3_end i3_start];
    % x values
    xp = [-120 -120 -100 -100];
    out = [p1; p2; p3];
end
if (phases == 5)
    i1_start = 76.94;
    i1_end = 42.19;
    i2_start = i1_end;
    i2_end = 31.59;
    i3_start = i2_end;
    i3_end = pos(startstop);
    i4_start = i3_end;
    i4_end = pos(endstop);
    i5_start = i4_end;
    i5_end = 12.5;
    % z values
    p1 = [i1_start i1_end i1_end i1_start];
    p2 = [i2_start i2_end i2_end i2_start];
    p3 = [i3_start i3_end i3_end i3_start];
    p4 = [i4_start i4_end i4_end i4_start];
    p5 = [i5_start i5_end i5_end i5_start];
    % x values
    xp = [-120 -120 -100 -100];
    out = [p1; p2; p3; p4; p5];
end
end