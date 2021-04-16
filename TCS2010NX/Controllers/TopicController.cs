using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TCS2010NX.Data;
using TCS2010NX.Models;

namespace TCS2010NX.Controllers
{
    public class TopicController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TopicController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Topic
        public async Task<IActionResult> Index()
        {
            return View(await _context.Topic.ToListAsync());
        }

        // GET: Topic/Details/5
        public async Task<IActionResult> Details(int? id, string error = "")
        {
            if (id == null)
            {
                return NotFound();
            }

            var topic = await _context.Topic
                .FirstOrDefaultAsync(m => m.Id == id);
            if (topic == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentContribution = await _context.Contribution.Include(c => c.Files)
                .FirstOrDefaultAsync(c => c.TopicId == id && c.ContributorId == userId);

            List<Comment> commets = null;
            if (currentContribution != null)
            {
                commets = await _context.Comment.Include(c => c.User)
                .Where(c => c.ContributionId == currentContribution.Id)
                .OrderBy(c => c.Date)
                .ToListAsync();       
            }

            ViewData["Error"] = error;
            ViewData["comments"] = commets;
            ViewData["currentContribution"] = currentContribution;


            return View(topic);
        }

        // GET: Topic/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Topic/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Topic topic)
        {
            if (ModelState.IsValid)
            {
                _context.Add(topic);
                await _context.SaveChangesAsync();

              
                var folderName = topic.Id.ToString();

                var path = Path.Combine( _Global.PATH_TOPIC, folderName);

                if (!Directory.Exists(path)) { Directory.CreateDirectory(path); }

                return RedirectToAction(nameof(Index));
            }

            return View(topic);
        }

        // GET: Topic/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) { return NotFound(); }

            var topic = await _context.Topic.FindAsync(id);

            if (topic == null) { return NotFound(); }

            return View(topic);
        }

        // POST: Topic/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Deadline_1")] Topic topic)
        {
            if (id != topic.Id) { return NotFound(); }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(topic);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TopicExists(topic.Id)) { return NotFound(); }
                    else { throw; }
                }

                return RedirectToAction(nameof(Index));
            }

            return View(topic);
        }

        // GET: Topic/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var topic = await _context.Topic.FirstOrDefaultAsync(m => m.Id == id);

            if (topic == null)
            {
                return NotFound();
            }

            return View(topic);
        }

        // POST: Topic/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var topic = await _context.Topic.FindAsync(id);
            _context.Topic.Remove(topic);
            await _context.SaveChangesAsync();

            
            var folderName = id.ToString();

            var path = Path.Combine( _Global.PATH_TOPIC, folderName);

            if (Directory.Exists(path)) { Directory.Delete(path); }

            return RedirectToAction(nameof(Index));
        }

        private bool TopicExists(int id)
        {
            return _context.Topic.Any(e => e.Id == id);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpLoadFile(Contribution contribution, IFormFile file, bool isAcceptTerms = false)
        {
            if (isAcceptTerms)
            {
            

                if (ModelState.IsValid)
                {
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    var user = await _context.Users.FindAsync(userId);
                    var existContribution = await _context.Contribution.FirstOrDefaultAsync(c => c.ContributorId == userId && c.TopicId == contribution.TopicId);
                    var topic = await _context.Topic.FindAsync(contribution.TopicId);

                    if (topic.Deadline_2 < DateTime.Now)
                    {
                        return RedirectToAction(nameof(Details), new { id = contribution.TopicId, error = "Deadline 2 is over." });
                    }
                    if (topic.Deadline_1 < DateTime.Now)
                    {
                        if (existContribution == null)
                        {
                            return RedirectToAction(nameof(Details), new { id = contribution.TopicId, error = "Deadline 1 is over." });
                        }
                    }


                    if (file.Length > 0)
                    {
                        if (existContribution == null)
                        {
                            contribution.ContributorId = userId;

                            contribution.Status = ContributionStatus.Pending;

                            _context.Add(contribution);
                            await _context.SaveChangesAsync();

                            existContribution = contribution;
                        }

                        else
                        {
                            existContribution.Status = ContributionStatus.Pending;

                            _context.Update(existContribution);
                            await _context.SaveChangesAsync();
                        }
                        FileType? fileType;
                        string fileExtension = Path.GetExtension(file.FileName).ToLower();

                        switch (fileExtension)
                        {
                            case ".doc": case ".docx": fileType = FileType.Document; break;
                            case ".jpg": case ".png": fileType = FileType.Image; break;
                            default: fileType = null; break;
                        }

                        if (fileType != null)
                        {

                            var path = Path.Combine(_Global.PATH_TOPIC, existContribution.TopicId.ToString(), user.Number);

                            if (!Directory.Exists(path)) { Directory.CreateDirectory(path); }

                            // Upload file
                            path = Path.Combine(path, String.Format("{0}.{1:yyyy-MM-dd.ss-mm-HH}{2}", user.Number, DateTime.Now, fileExtension));
                            using var stream = new FileStream(path, FileMode.Create);
                            file.CopyTo(stream);

                            var newFile = new SubmittedFile();
                            newFile.ContributionId = existContribution.Id;
                            newFile.URL = path;
                            newFile.Type = (FileType)fileType;

                            _context.Add(newFile);
                            await _context.SaveChangesAsync();
                        }
                    }

                }
            }
            else
            
                return RedirectToAction(nameof(Details), new { id = contribution.TopicId, error = "You must accept Terms." });
            
            return RedirectToAction(nameof(Details), new {  id = contribution.TopicId});
        }

        public async Task<IActionResult> DownloadFile(int fileId= -1)
        {
            var file = await _context.File.FindAsync(fileId);
            byte[] fileBytes = System.IO.File.ReadAllBytes(file.URL);

            return File(fileBytes, System.Net.Mime.MediaTypeNames.Application.Octet, file.URL.Split("\\").Last());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Comment(int topicId, string commentContent)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (ModelState.IsValid)
            {
                var user = await _context.Users.FindAsync(userId);
                var existContribution = await _context.Contribution.FirstOrDefaultAsync(c => c.ContributorId == userId && c.TopicId == topicId);

                if (existContribution != null && !String.IsNullOrEmpty(commentContent))
                {
                    var comment = new Comment();

                    comment.UserId = userId;
                    comment.Content = commentContent;
                    comment.Date = DateTime.Now;
                    comment.ContributionId = existContribution.Id;

                    _context.Add(comment);
                    await _context.SaveChangesAsync();

                }


            }
            return RedirectToAction(nameof(Details), new { id = topicId });
        }
    }
}